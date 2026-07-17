using System.Diagnostics;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace ShellDocs.CLI.Commands;

/* `shelldocs build` — `dotnet publish -c Release` then post-process the
   output for static hosts (GH Pages / Cloudflare / S3). Detects the
   published `wwwroot/` for Blazor WASM projects and copies it to --output;
   for server projects, copies the whole publish directory instead.

   Post-processing:
   - --base-href rewrites <base href="/" /> in index.html (for GH Pages subpaths).
   - --spa-fallback copies index.html to 404.html (GH Pages SPA-routing trick). */
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

        var outputAbs = Path.GetFullPath(Path.Combine(root, output));
        var publishStage = Path.Combine(root, "obj", "shelldocs-publish");

        AnsiConsole.MarkupLine($"[dim]shelldocs build →[/] [cyan]{Path.GetFileName(csproj)}[/]");
        AnsiConsole.MarkupLine($"[dim]output:[/] [cyan]{outputAbs}[/]");
        if (baseHref is not null)  AnsiConsole.MarkupLine($"[dim]base href:[/] [cyan]{baseHref}[/]");
        if (spaFallback)           AnsiConsole.MarkupLine("[dim]spa fallback:[/] [cyan]index.html → 404.html[/]");
        AnsiConsole.WriteLine();

        // 1. dotnet publish to a scratch dir
        var publishExit = RunPublish(csproj, publishStage);
        if (publishExit != 0) return publishExit;

        // 2. Locate static payload: wwwroot for WASM, whole dir for Server
        var wwwroot = Path.Combine(publishStage, "wwwroot");
        var source = Directory.Exists(wwwroot) ? wwwroot : publishStage;
        var kind = Directory.Exists(wwwroot) ? "static (Blazor WASM)" : "server (needs a .NET host)";
        AnsiConsole.MarkupLine($"[dim]publish kind:[/] [cyan]{kind}[/]");

        // 3. Copy to output (clean first so stale files never linger)
        if (Directory.Exists(outputAbs)) Directory.Delete(outputAbs, recursive: true);
        CopyDirectory(source, outputAbs);

        // 4. Post-process
        var indexHtml = Path.Combine(outputAbs, "index.html");
        if (baseHref is not null && File.Exists(indexHtml))
        {
            RewriteBaseHref(indexHtml, baseHref);
        }
        if (spaFallback && File.Exists(indexHtml))
        {
            File.Copy(indexHtml, Path.Combine(outputAbs, "404.html"), overwrite: true);
        }

        // Cleanup scratch
        try { Directory.Delete(publishStage, recursive: true); } catch { }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓[/] built to [cyan]{outputAbs}[/]");
        return 0;
    }

    private static int RunPublish(string csproj, string publishDir)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
        };
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

    // Recursive directory copy — no built-in in .NET stdlib.
    internal static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var subdir in Directory.GetDirectories(source))
        {
            CopyDirectory(subdir, Path.Combine(dest, Path.GetFileName(subdir)));
        }
    }

    /* Rewrites <base href="..." /> in index.html. `baseHref` should include
       leading + trailing slashes ("/repo-name/"). Handles single, double,
       and no-quote variants. */
    internal static void RewriteBaseHref(string indexHtml, string baseHref)
    {
        var html = File.ReadAllText(indexHtml);
        var patched = Regex.Replace(
            html,
            @"<base\s+href\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)\s*/?>",
            $"<base href=\"{baseHref}\" />",
            RegexOptions.IgnoreCase);
        File.WriteAllText(indexHtml, patched);
    }

    private static string? FindCsproj(string dir)
    {
        var matches = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
        return matches.Length == 0 ? null : matches[0];
    }
}
