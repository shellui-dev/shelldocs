using System.Diagnostics;
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
        // Port 0 → kernel picks a free port at Kestrel bind time (no TOCTOU
        // gap between us checking and the child using). We learn the actual
        // port by parsing Kestrel's "Now listening on:" startup line below.
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
                        AnsiConsole.MarkupLine($"  [yellow]warn:[/] [cyan]{url}[/] returned [yellow]{(int)response.StatusCode}[/]");
                        failed++;
                        continue;
                    }
                    var html = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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
            return new Result(failed == 0, rendered, failed);
        }
        finally
        {
            if (proc is not null) TryKill(proc);
        }
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
