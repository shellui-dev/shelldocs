using System.Text.RegularExpressions;
using ShellDocs.Templates;
using Spectre.Console;

namespace ShellDocs.CLI.Commands;

/* `shelldocs init` — detects the Blazor project in the current directory,
   adds ShellDocs package references, and drops in scaffolding for content/
   plus a DocsPage.razor route. Copy-paste snippets for Program.cs + App.razor
   land in SHELLDOCS_SETUP.md next to the .csproj rather than being patched
   in — the user's project may have auth, custom middleware, etc. we can't
   safely rewrite around.

   Idempotent: every file/package check is skip-if-present. */
internal static class InitCommand
{
    private const string ShellDocsVersion = "0.1.0-alpha";

    public static int Run(string dir, bool yes, string theme)
    {
        var root = Path.GetFullPath(dir);
        if (!Directory.Exists(root))
        {
            AnsiConsole.MarkupLine($"[red]error:[/] directory not found: [yellow]{root}[/]");
            return 1;
        }

        var csproj = FindCsproj(root);
        if (csproj is null)
        {
            AnsiConsole.MarkupLine($"[red]error:[/] no .csproj found in [yellow]{root}[/]");
            AnsiConsole.MarkupLine("[dim]Run this inside a Blazor project root.[/]");
            return 1;
        }

        if (!IsBlazorProject(csproj))
        {
            AnsiConsole.MarkupLine($"[red]error:[/] [yellow]{Path.GetFileName(csproj)}[/] doesn't look like a Blazor project.");
            AnsiConsole.MarkupLine("[dim]Expected SDK Microsoft.NET.Sdk.Web or Microsoft.NET.Sdk.Razor with an AspNetCore.Components reference.[/]");
            return 1;
        }

        var siteName = InferSiteName(csproj);
        var githubRepo = yes ? "" : PromptGithub();

        AnsiConsole.WriteLine();
        var summary = new Table().Border(TableBorder.Rounded).AddColumn("").AddColumn("");
        summary.HideHeaders();
        summary.AddRow("[bold]Project[/]", $"[yellow]{Path.GetFileName(csproj)}[/]");
        summary.AddRow("[bold]Site name[/]", $"[yellow]{siteName}[/]");
        summary.AddRow("[bold]Theme[/]", $"[yellow]{theme}[/]");
        AnsiConsole.Write(summary);
        AnsiConsole.WriteLine();

        var changes = new List<string>();

        // 1. Package references
        AddPackageIfMissing(csproj, "ShellDocs.Components", ShellDocsVersion, changes);
        AddPackageIfMissing(csproj, "ShellDocs.Tokens",     ShellDocsVersion, changes);

        // 2. content/docs/ scaffolding
        var contentDir = Path.Combine(root, "content", "docs");
        Directory.CreateDirectory(contentDir);
        WriteIfMissing(Path.Combine(contentDir, "introduction.md"), ScaffoldTemplates.IntroductionMd, changes);
        WriteIfMissing(Path.Combine(contentDir, "meta.json"),       ScaffoldTemplates.MetaJson, changes);

        // 3. Components/Pages/DocsPage.razor — the routed page that binds the framework to /docs/*
        var pagesDir = LocateOrCreatePagesDir(root);
        WriteIfMissing(Path.Combine(pagesDir, "DocsPage.razor"), ScaffoldTemplates.DocsPageRazor, changes);

        // 4. Setup instructions with the Program.cs + App.razor snippets
        var setupPath = Path.Combine(root, "SHELLDOCS_SETUP.md");
        WriteIfMissing(setupPath, ScaffoldTemplates.SetupInstructionsMd(siteName, githubRepo), changes);

        // Report
        AnsiConsole.WriteLine();
        if (changes.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]✓[/] Already initialised — nothing to do.");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Wrote [bold]{changes.Count}[/] change(s):");
            foreach (var c in changes) AnsiConsole.MarkupLine($"  [dim]•[/] {c}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Next:[/] follow the snippets in [yellow]SHELLDOCS_SETUP.md[/] to patch Program.cs and App.razor, then:");
        AnsiConsole.MarkupLine("  [dim]$[/] [cyan]dotnet run[/]");
        AnsiConsole.MarkupLine("  [dim]→ visit[/] [cyan]/docs/introduction[/]");
        return 0;
    }

    private static string? FindCsproj(string dir)
    {
        var matches = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly);
        return matches.Length == 0 ? null : matches[0];
    }

    private static bool IsBlazorProject(string csproj)
    {
        var xml = File.ReadAllText(csproj);
        var hasWebSdk = xml.Contains("Sdk=\"Microsoft.NET.Sdk.Web\"", StringComparison.OrdinalIgnoreCase)
                     || xml.Contains("Sdk=\"Microsoft.NET.Sdk.Razor\"", StringComparison.OrdinalIgnoreCase)
                     || xml.Contains("Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\"", StringComparison.OrdinalIgnoreCase);
        var hasBlazorRef = xml.Contains("Microsoft.AspNetCore.Components", StringComparison.OrdinalIgnoreCase);
        return hasWebSdk || hasBlazorRef;
    }

    private static void AddPackageIfMissing(string csproj, string package, string version, List<string> changes)
    {
        var xml = File.ReadAllText(csproj);
        var pattern = new Regex($@"<PackageReference\s+Include=""{Regex.Escape(package)}""", RegexOptions.IgnoreCase);
        if (pattern.IsMatch(xml))
        {
            // Already there.
            return;
        }

        var reference = $"    <PackageReference Include=\"{package}\" Version=\"{version}\" />";

        // Prefer inserting into an existing ItemGroup that already holds PackageReferences.
        var itemGroup = Regex.Match(xml,
            @"<ItemGroup>\s*(?=\s*<PackageReference)",
            RegexOptions.IgnoreCase);
        string patched;
        if (itemGroup.Success)
        {
            var insertAt = itemGroup.Index + itemGroup.Length;
            patched = xml.Insert(insertAt, reference + Environment.NewLine + "  ");
        }
        else
        {
            // Fall back: new ItemGroup before </Project>.
            var closing = xml.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
            if (closing < 0) return;
            var block = $"  <ItemGroup>{Environment.NewLine}{reference}{Environment.NewLine}  </ItemGroup>{Environment.NewLine}{Environment.NewLine}";
            patched = xml.Insert(closing, block);
        }

        File.WriteAllText(csproj, patched);
        changes.Add($"added [cyan]{package}[/] to {Path.GetFileName(csproj)}");
    }

    private static string LocateOrCreatePagesDir(string root)
    {
        // Common Blazor project layouts: Components/Pages (Web App), Pages (Server / WASM classic).
        var candidates = new[]
        {
            Path.Combine(root, "Components", "Pages"),
            Path.Combine(root, "Pages"),
        };
        foreach (var c in candidates)
        {
            if (Directory.Exists(c)) return c;
        }
        // Default to Components/Pages (modern Blazor Web App layout).
        var chosen = candidates[0];
        Directory.CreateDirectory(chosen);
        return chosen;
    }

    private static void WriteIfMissing(string path, string content, List<string> changes)
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        var rel = Path.GetRelativePath(Directory.GetCurrentDirectory(), path);
        changes.Add($"created [cyan]{rel}[/]");
    }

    private static string InferSiteName(string csproj)
    {
        // Use the .csproj filename minus extension as the default site name.
        var name = Path.GetFileNameWithoutExtension(csproj);
        return string.IsNullOrEmpty(name) ? "Docs" : name;
    }

    private static string PromptGithub()
    {
        try
        {
            return AnsiConsole.Prompt(
                new TextPrompt<string>("[bold]GitHub repo[/] [dim](owner/repo, blank to skip)[/]:")
                    .AllowEmpty());
        }
        catch
        {
            // Non-interactive environment (redirected stdin, CI without a TTY).
            return "";
        }
    }
}
