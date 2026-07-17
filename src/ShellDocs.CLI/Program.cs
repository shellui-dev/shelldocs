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
        var dir = new Option<string>("--dir")
        {
            Description = "Project directory to initialise (default: current dir).",
            DefaultValueFactory = _ => Directory.GetCurrentDirectory()
        };
        var yes = new Option<bool>("--yes") { Description = "Non-interactive mode with default options." };
        var theme = new Option<string>("--theme")
        {
            Description = "Theme preset: shadcn, fuma, nextra.",
            DefaultValueFactory = _ => "shadcn"
        };
        var cmd = new Command("init", "Initialize ShellDocs in a Blazor project — adds packages, generates content/ and DocsPage.razor, emits Program.cs + App.razor snippets.")
        {
            dir, yes, theme
        };
        cmd.SetAction(pr =>
        {
            AnsiConsole.Markup($"[blue]{Logo}[/]");
            AnsiConsole.MarkupLine("[dim]     the docs framework for .NET[/]");
            AnsiConsole.WriteLine();
            return InitCommand.Run(
                pr.GetValue(dir) ?? Directory.GetCurrentDirectory(),
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
        var port = new Option<int>("--port")
        {
            Description = "Port to bind on.",
            DefaultValueFactory = _ => 5000
        };
        var cmd = new Command("dev", "Start dev server with hot-reload for .razor / .cs / .md changes.") { port };
        cmd.SetAction(_ =>
        {
            AnsiConsole.MarkupLine("[yellow]shelldocs dev[/] — not yet implemented (feat/cli-dev-build).");
        });
        return cmd;
    }

    private static Command CreateBuildCommand()
    {
        var output = new Option<string>("--output")
        {
            Description = "Output directory.",
            DefaultValueFactory = _ => "publish"
        };
        var cmd = new Command("build", "Produce a static site ready for GH Pages / Vercel / Netlify.") { output };
        cmd.SetAction(_ =>
        {
            AnsiConsole.MarkupLine("[yellow]shelldocs build[/] — not yet implemented (feat/cli-dev-build).");
        });
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
