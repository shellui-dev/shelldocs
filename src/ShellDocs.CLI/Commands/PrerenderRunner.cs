using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace ShellDocs.CLI.Commands;

internal static class PrerenderRunner
{
    public record Result(bool Success, int Rendered, int Failed);

    // Match Kestrel's startup line: "Now listening on: http://127.0.0.1:63593"
    private static readonly Regex KestrelListening = new(
        @"Now listening on:\s*(?<url>https?://\S+)", RegexOptions.Compiled);

    public static Result Run(string publishDir, string csproj, IReadOnlyList<string> urls, string outputDir, TimeSpan? readyTimeout = null)
    {
        var assemblyName = InferAssemblyName(csproj) + ".dll";
        var assemblyPath = Path.Combine(publishDir, assemblyName);
        if (!File.Exists(assemblyPath))
        {
            AnsiConsole.MarkupLine($"[red]error:[/] published assembly not found at [yellow]{assemblyPath}[/]");
            return new Result(false, 0, 0);
        }

        AnsiConsole.MarkupLine($"[dim]prerender:[/] launching [cyan]{assemblyName}[/]");

        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = publishDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(assemblyPath);
        // Port 0 lets Kestrel pick a free port (no check-then-bind race); the real
        // port comes from its "Now listening on:" line.
        psi.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        psi.Environment["DOTNET_USE_POLLING_FILE_WATCHER"] = "0";

        Process? proc = null;
        try
        {
            proc = Process.Start(psi);
            if (proc is null)
            {
                AnsiConsole.MarkupLine("[red]error:[/] failed to start published app");
                return new Result(false, 0, 0);
            }

            // Cover crash / Ctrl+C paths in addition to the finally block.
            var procRef = proc;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => TryKill(procRef);
            Console.CancelKeyPress += (_, _) => TryKill(procRef);

            using var listeningEvent = new ManualResetEventSlim(false);
            string? boundUrl = null;

            void OnStreamLine(string line)
            {
                if (boundUrl is null)
                {
                    var m = KestrelListening.Match(line);
                    if (m.Success)
                    {
                        boundUrl = m.Groups["url"].Value.TrimEnd('/');
                        listeningEvent.Set();
                    }
                }
            }

            _ = Task.Run(() => ReadLines(proc.StandardOutput, OnStreamLine));
            _ = Task.Run(() => ReadLines(proc.StandardError, OnStreamLine));

            if (!listeningEvent.Wait(readyTimeout ?? TimeSpan.FromSeconds(30)) || boundUrl is null)
            {
                AnsiConsole.MarkupLine("[red]error:[/] published app never announced a listening port within timeout");
                return new Result(false, 0, 0);
            }

            AnsiConsole.MarkupLine($"[dim]prerender:[/] app listening on [cyan]{boundUrl}[/]");

            using var http = new HttpClient { BaseAddress = new Uri(boundUrl), Timeout = TimeSpan.FromSeconds(30) };
            var rendered = 0;
            var failed = 0;

            foreach (var url in urls.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var response = http.GetAsync(url).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        var why = response.StatusCode == HttpStatusCode.NotFound ? "404 (page not found)" : ((int)response.StatusCode).ToString();
                        AnsiConsole.MarkupLine($"  [yellow]warn:[/] [cyan]{url}[/] returned [yellow]{why}[/]");
                        failed++;
                        continue;
                    }
                    var html = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    // A docs page that rendered <DocsNotFound> where the status couldn't be set.
                    if (IsNotFoundPage(html))
                    {
                        AnsiConsole.MarkupLine($"  [yellow]warn:[/] [cyan]{url}[/] rendered [yellow]Page not found[/]");
                        failed++;
                        continue;
                    }
                    var outPath = UrlToFilePath(url, outputDir);
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                    File.WriteAllText(outPath, html);
                    rendered++;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"  [red]error:[/] [cyan]{url}[/] failed: {ex.Message}");
                    failed++;
                }
            }

            AnsiConsole.MarkupLine($"[dim]prerender:[/] wrote [green]{rendered}[/] page(s)" + (failed > 0 ? $", [yellow]{failed}[/] failed" : ""));

            var redirects = WriteRedirects(http, outputDir);
            if (redirects > 0) AnsiConsole.MarkupLine($"[dim]redirects:[/] wrote [cyan]{redirects}[/] redirect page(s)");
            if (WriteNotFoundPage(http, outputDir)) AnsiConsole.MarkupLine("[dim]not found:[/] wrote [cyan]404.html[/]");
            return new Result(failed == 0, rendered, failed);
        }
        finally
        {
            if (proc is not null) TryKill(proc);
        }
    }

    internal const string NotFoundMarker = "data-shelldocs-not-found";
    // A URL no site has, so the docs route renders <DocsNotFound>.
    internal const string NotFoundProbe = "/docs/__shelldocs-not-found__";

    internal static bool IsNotFoundPage(string html) => html.Contains(NotFoundMarker, StringComparison.Ordinal);

    // Static hosts (GitHub Pages, Netlify, Cloudflare Pages) serve 404.html for unknown
    // URLs. Written from the site's own not-found view, when its docs route has one.
    private static bool WriteNotFoundPage(HttpClient http, string outputDir)
    {
        try
        {
            var response = http.GetAsync(NotFoundProbe).GetAwaiter().GetResult();
            var html = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (response.StatusCode != HttpStatusCode.NotFound || !IsNotFoundPage(html)) return false;
            File.WriteAllText(Path.Combine(outputDir, "404.html"), html);
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [yellow]warn:[/] couldn't render the not-found page: {ex.Message}");
            return false;
        }
    }

    private record RedirectEntry(string From, string To);

    // The app serves its redirect map (DocsRedirectMiddleware); each source URL without
    // a prerendered page gets a page that forwards to the target. An app on an older
    // ShellDocs, or with redirects off, answers 404 and nothing is written.
    private static int WriteRedirects(HttpClient http, string outputDir)
    {
        List<RedirectEntry>? entries;
        try
        {
            var response = http.GetAsync("/_shelldocs/redirects.json").GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return 0;
            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            entries = JsonSerializer.Deserialize<List<RedirectEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [yellow]warn:[/] couldn't read the redirect map: {ex.Message}");
            return 0;
        }

        var written = 0;
        foreach (var entry in entries ?? new())
        {
            var outPath = UrlToFilePath(entry.From, outputDir);
            if (File.Exists(outPath)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllText(outPath, RedirectPage(entry.To));
            written++;
        }
        return written;
    }

    // Target is base-relative so `--base-href` (which rewrites <base>) keeps it right.
    internal static string RedirectPage(string to)
    {
        var relative = to.TrimStart('/');
        var attr = WebUtility.HtmlEncode(relative);
        var js = JsonSerializer.Serialize(relative);
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8" />
            <base href="/" />
            <title>Redirecting…</title>
            <meta name="robots" content="noindex" />
            <meta http-equiv="refresh" content="0; url={{attr}}" />
            <script>location.replace(new URL({{js}}, document.baseURI).href + location.search + location.hash);</script>
            </head>
            <body><a href="{{attr}}">Redirecting…</a></body>
            </html>
            """;
    }

    private static string InferAssemblyName(string csproj)
    {
        try
        {
            var xml = File.ReadAllText(csproj);
            var m = Regex.Match(xml, @"<AssemblyName>\s*(?<name>[^<\s]+)\s*</AssemblyName>", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups["name"].Value;
        }
        catch { }
        return Path.GetFileNameWithoutExtension(csproj);
    }

    // "/docs/introduction" → <output>/docs/introduction/index.html so static
    // hosts serve /docs/introduction/ without a .html suffix.
    private static string UrlToFilePath(string url, string outputDir)
    {
        var trimmed = url.Trim('/');
        if (string.IsNullOrEmpty(trimmed))
            return Path.Combine(outputDir, "index.html");
        var segments = trimmed.Split('/');
        var relative = Path.Combine(segments);
        return Path.Combine(outputDir, relative, "index.html");
    }

    private static void TryKill(Process proc)
    {
        try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); }
        catch { }
    }

    // Reads until EOF, handing each line to onLine. Keeps reading past the
    // "listening" match so the child's pipes never fill up and deadlock.
    private static void ReadLines(StreamReader reader, Action<string> onLine)
    {
        try
        {
            string? line;
            while ((line = reader.ReadLine()) is not null) onLine(line);
        }
        catch { }
    }
}
