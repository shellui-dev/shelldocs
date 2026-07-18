using System.CommandLine;
using ShellDocs.CLI.Commands;
using Spectre.Console;

namespace ShellDocs.CLI;

/* CLI entry point.
   This scaffolding wires up the command tree so `shelldocs --help` produces the right shape. */
internal class Program
{
    private const string Logo = @"
 ███████╗██╗  ██╗███████╗██╗     ██╗     ██████╗  ██████╗  ██████╗███████╗
 ██╔════╝██║  ██║██╔════╝██║     ██║     ██╔══██╗██╔═══██╗██╔════╝██╔════╝
 ███████╗███████║█████╗  ██║     ██║     ██║  ██║██║   ██║██║     ███████╗
 ╚════██║██╔══██║██╔══╝  ██║     ██║     ██║  ██║██║   ██║██║     ╚════██║
 ███████║██║  ██║███████╗███████╗███████╗██████╔╝╚██████╔╝╚██████╗███████║
 ╚══════╝╚═╝  ╚═╝╚══════╝╚══════╝╚══════╝╚═════╝  ╚═════╝  ╚═════╝╚══════╝
";

    private static int Main(string[] args)
    {
        var root = new RootCommand("ShellDocs — the docs framework for .NET.");
        root.Subcommands.Add(CreateInitCommand());
        root.Subcommands.Add(CreateNewCommand());
        root.Subcommands.Add(CreateDevCommand());
        root.Subcommands.Add(CreateBuildCommand());
        root.Subcommands.Add(CreatePreviewCommand());
        return root.Parse(args).Invoke();
    }

    private static Command CreateInitCommand()
    {
        var path = new Argument<string?>("path")
        {
            Description = "Target directory for the new docs project (default: docs/<CwdName>.Docs). Ignored with --attach.",
            Arity = ArgumentArity.ZeroOrOne
        };
        var dir = new Option<string>("--dir")
        {
            Description = "Working directory (default: current dir). With --attach, the project directory.",
            DefaultValueFactory = _ => Directory.GetCurrentDirectory()
        };
        var attach = new Option<bool>("--attach")
        {
            Description = "Augment an existing Blazor project in --dir instead of creating a new one. Emits SHELLDOCS_SETUP.md for manual Program.cs / App.razor patching."
        };
        var yes = new Option<bool>("--yes") { Description = "Non-interactive mode with default options." };
        var theme = new Option<string>("--theme")
        {
            Description = "Theme preset: shadcn, fuma, nextra.",
            DefaultValueFactory = _ => "shadcn"
        };
        var cmd = new Command("init", "Scaffold a new ShellDocs site (default) or attach to an existing Blazor project.")
        {
            path, dir, attach, yes, theme
        };
        cmd.SetAction(pr =>
        {
            AnsiConsole.Markup($"[blue]{Logo}[/]");
            AnsiConsole.MarkupLine("[dim]     the docs framework for .NET[/]");
            AnsiConsole.WriteLine();
            return InitCommand.Run(
                pr.GetValue(path),
                pr.GetValue(dir) ?? Directory.GetCurrentDirectory(),
                pr.GetValue(attach),
                pr.GetValue(yes),
                pr.GetValue(theme) ?? "shadcn");
        });
        return cmd;
    }

    private static Command CreateNewCommand()
    {
        var kind = new Argument<string>("kind") { Description = "Template kind: page, component-page." };
        var name = new Argument<string>("name") { Description = "File name for the new page." };
        var cmd = new Command("new", "Scaffold a new doc page from a template.") { kind, name };
        cmd.SetAction(pr =>
        {
            AnsiConsole.MarkupLine($"[yellow]shelldocs new {pr.GetValue(kind)} {pr.GetValue(name)}[/] — not yet implemented (feat/cli-init).");
        });
        return cmd;
    }

    private static Command CreateDevCommand()
    {
        var dir = new Option<string>("--dir")
        {
            Description = "Project directory (default: current dir).",
            DefaultValueFactory = _ => Directory.GetCurrentDirectory()
        };
        var port = new Option<int>("--port")
        {
            Description = "Port to bind on.",
            DefaultValueFactory = _ => 5000
        };
        var cmd = new Command("dev", "Start dev server with hot-reload for .razor / .cs / .md changes.")
        {
            dir, port
        };
        cmd.SetAction(pr =>
            DevCommand.Run(
                pr.GetValue(dir) ?? Directory.GetCurrentDirectory(),
                pr.GetValue(port)));
        return cmd;
    }

    private static Command CreateBuildCommand()
    {
        var dir = new Option<string>("--dir")
        {
            Description = "Project directory (default: current dir).",
            DefaultValueFactory = _ => Directory.GetCurrentDirectory()
        };
        var output = new Option<string>("--output")
        {
            Description = "Output directory for the static site.",
            DefaultValueFactory = _ => "publish"
        };
        var baseHref = new Option<string?>("--base-href")
        {
            Description = "Rewrite <base href> in index.html (e.g. \"/my-repo/\" for GH Pages subpaths)."
        };
        var spaFallback = new Option<bool>("--spa-fallback")
        {
            Description = "Copy index.html → 404.html so client-side routes survive on GH Pages."
        };
        var cmd = new Command("build", "Produce a static site ready for GH Pages / Cloudflare / S3.")
        {
            dir, output, baseHref, spaFallback
        };
        cmd.SetAction(pr =>
            BuildCommand.Run(
                pr.GetValue(dir) ?? Directory.GetCurrentDirectory(),
                pr.GetValue(output) ?? "publish",
                pr.GetValue(baseHref),
                pr.GetValue(spaFallback)));
        return cmd;
    }

    private static Command CreatePreviewCommand()
    {
        var name = new Argument<string>("name") { Description = "Component name to preview." };
        var cmd = new Command("preview", "Render a single component in isolation for design review.") { name };
        cmd.SetAction(pr =>
        {
            AnsiConsole.MarkupLine($"[yellow]shelldocs preview {pr.GetValue(name)}[/] — not yet implemented.");
        });
        return cmd;
    }
}
