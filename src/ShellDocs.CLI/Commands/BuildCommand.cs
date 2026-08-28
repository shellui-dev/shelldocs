using System.Diagnostics;
using System.Text.RegularExpressions;
using ShellDocs.Core;
using Spectre.Console;

namespace ShellDocs.CLI.Commands;

internal static class BuildCommand
{
    public static int Run(string dir, string output, string? baseHref, bool spaFallback)
    {
        var root = Path.GetFullPath(dir);
        var csproj = FindCsproj(root);
        if (csproj is null)
        {
            AnsiConsole.MarkupLine($"[red]error:[/] no .csproj found in [yellow]{root}[/]");
            return 1;
        }

        var contentRoot = Path.Combine(root, "content");
        if (!Directory.Exists(contentRoot))
        {
            AnsiConsole.MarkupLine($"[red]error:[/] no [yellow]content/[/] directory found under [yellow]{root}[/]");
            AnsiConsole.MarkupLine("[dim]shelldocs build needs a docs project with content/ so it can enumerate URLs to prerender.[/]");
            return 1;
        }

        var outputAbs = Path.GetFullPath(Path.Combine(root, output));
        var publishStage = Path.Combine(root, "obj", "shelldocs-publish");

        AnsiConsole.MarkupLine($"[dim]shelldocs build →[/] [cyan]{Path.GetFileName(csproj)}[/]");
        AnsiConsole.MarkupLine($"[dim]output:[/] [cyan]{outputAbs}[/]");
        if (baseHref is not null)  AnsiConsole.MarkupLine($"[dim]base href:[/] [cyan]{baseHref}[/]");
        if (spaFallback)           AnsiConsole.MarkupLine("[dim]spa fallback:[/] [cyan]index.html → 404.html[/]");
        AnsiConsole.WriteLine();

        var publishExit = RunPublish(csproj, publishStage);
        if (publishExit != 0) return publishExit;

        // Belt-and-suspenders: mirror source content/ into publish. Consumer
        // csprojs using `<Content Update="content/...">` don't actually copy
        // .md files (Update is the wrong verb; see InitCommand notes), which
        // would leave the running app with an empty NavigationGraph and every
        // prerendered page as "Page not found."
        var publishContent = Path.Combine(publishStage, "content");
        if (!Directory.Exists(publishContent))
        {
            AnsiConsole.MarkupLine("[dim]content:[/] publish output has no content/ — mirroring from source");
            CopyDirectoryOverwriting(contentRoot, publishContent);
        }

        // Home ("/") is served by Home.razor and isn't part of NavigationGraph.
        var urls = new List<string> { "/" };
        try
        {
            var graph = NavigationGraphBuilder.Build(contentRoot);
            urls.AddRange(graph.AllUrls);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]error:[/] failed to read content/ nav graph: {ex.Message}");
            return 1;
        }
        AnsiConsole.MarkupLine($"[dim]prerender:[/] discovered [cyan]{urls.Count}[/] URL(s) from content/");

        if (Directory.Exists(outputAbs)) Directory.Delete(outputAbs, recursive: true);
        Directory.CreateDirectory(outputAbs);

        var result = PrerenderRunner.Run(publishStage, csproj, urls, outputAbs);
        if (!result.Success)
        {
            AnsiConsole.MarkupLine("[red]error:[/] prerender did not complete cleanly. Output may be incomplete.");
            return 1;
        }

        var wwwroot = Path.Combine(publishStage, "wwwroot");
        if (Directory.Exists(wwwroot))
        {
            CopyDirectoryMerging(wwwroot, outputAbs);
            AnsiConsole.MarkupLine($"[dim]assets:[/] copied [cyan]wwwroot/[/] into output");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]warn:[/] publish/wwwroot/ not found — output may be missing framework assets");
        }

        if (baseHref is not null)
        {
            var patched = RewriteBaseHrefInAllHtml(outputAbs, baseHref);
            AnsiConsole.MarkupLine($"[dim]base-href:[/] rewrote [cyan]{patched}[/] HTML file(s)");
        }
        if (spaFallback)
        {
            var rootIndex = Path.Combine(outputAbs, "index.html");
            if (File.Exists(rootIndex))
            {
                File.Copy(rootIndex, Path.Combine(outputAbs, "404.html"), overwrite: true);
                AnsiConsole.MarkupLine("[dim]spa fallback:[/] wrote 404.html");
            }
        }

        try { Directory.Delete(publishStage, recursive: true); } catch { }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓[/] built to [cyan]{outputAbs}[/]");
        return 0;
    }

    private static int RunPublish(string csproj, string publishDir)
    {
        AnsiConsole.MarkupLine($"[dim]$[/] [cyan]dotnet publish -c Release -o {publishDir}[/]");
        var psi = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        psi.ArgumentList.Add("publish");
        psi.ArgumentList.Add(csproj);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("Release");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(publishDir);

        using var proc = Process.Start(psi);
        if (proc is null)
        {
            AnsiConsole.MarkupLine("[red]error:[/] failed to start dotnet");
            return 1;
        }
        proc.WaitForExit();
        return proc.ExitCode;
    }

    // Never overwrites — protects prerendered HTML sitting in the output tree.
    internal static void CopyDirectoryMerging(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            var target = Path.Combine(dest, Path.GetFileName(file));
            if (!File.Exists(target)) File.Copy(file, target);
        }
        foreach (var subdir in Directory.GetDirectories(source))
        {
            CopyDirectoryMerging(subdir, Path.Combine(dest, Path.GetFileName(subdir)));
        }
    }

    internal static void CopyDirectoryOverwriting(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var subdir in Directory.GetDirectories(source))
        {
            CopyDirectoryOverwriting(subdir, Path.Combine(dest, Path.GetFileName(subdir)));
        }
    }

    // `baseHref` must include leading + trailing slashes ("/repo-name/").
    internal static int RewriteBaseHrefInAllHtml(string outputDir, string baseHref)
    {
        var count = 0;
        foreach (var html in Directory.EnumerateFiles(outputDir, "*.html", SearchOption.AllDirectories))
        {
            var original = File.ReadAllText(html);
            var patched = Regex.Replace(
                original,
                @"<base\s+href\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)\s*/?>",
                $"<base href=\"{baseHref}\" />",
                RegexOptions.IgnoreCase);
            if (patched != original)
            {
                File.WriteAllText(html, patched);
                count++;
            }
        }
        return count;
    }

    private static string? FindCsproj(string dir)
    {
        var matches = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
        return matches.Length == 0 ? null : matches[0];
    }
}
