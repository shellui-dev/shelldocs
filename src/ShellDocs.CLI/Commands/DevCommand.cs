using System.Diagnostics;
using Spectre.Console;

namespace ShellDocs.CLI.Commands;

// `shelldocs dev` — thin wrapper around `dotnet watch run` that also asks
// MSBuild to include markdown under content/ in the watch set, so editing
// markdown triggers the navigation-graph rebuild on hot-reload.
internal static class DevCommand
{
    public static int Run(string dir, int port)
    {
        var root = Path.GetFullPath(dir);
        var csproj = FindCsproj(root);
        if (csproj is null)
        {
            AnsiConsole.MarkupLine($"[red]error:[/] no .csproj found in [yellow]{root}[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[dim]shelldocs dev →[/] [cyan]{Path.GetFileName(csproj)}[/] on [cyan]http://localhost:{port}[/]");
        AnsiConsole.MarkupLine("[dim]watching:[/] .cs, .razor, .css, .js, content/**/*.md");
        AnsiConsole.WriteLine();

        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(csproj)!,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("watch");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add(csproj);
        // MSBuild property picked up by dotnet-watch >= 8 to extend the watch set.
        psi.ArgumentList.Add("--non-interactive");
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--urls");
        psi.ArgumentList.Add($"http://localhost:{port}");

        // Forward Ctrl+C to the child so `dotnet watch` shuts down cleanly.
        using var proc = Process.Start(psi);
        if (proc is null)
        {
            AnsiConsole.MarkupLine("[red]error:[/] failed to start dotnet");
            return 1;
        }
        Console.CancelKeyPress += (_, ev) =>
        {
            ev.Cancel = true;
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
        };
        proc.WaitForExit();
        return proc.ExitCode;
    }

    private static string? FindCsproj(string dir)
    {
        var matches = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
        return matches.Length == 0 ? null : matches[0];
    }
}
