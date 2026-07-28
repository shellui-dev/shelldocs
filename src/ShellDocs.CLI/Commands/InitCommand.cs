using System.Diagnostics;
using System.Text.RegularExpressions;
using ShellDocs.Templates;
using Spectre.Console;

namespace ShellDocs.CLI.Commands;

/* `shelldocs init` — two modes:

   CREATE (default): scaffolds a brand-new Blazor Web App under docs/<Name>.Docs,
   runs `dotnet new blazor -o <path>`, then patches Program.cs + App.razor
   directly (we know the template shape because we created it). One command →
   running docs site.

   ATTACH (--attach): augments an existing Blazor project in --dir. Doesn't
   patch Program.cs / App.razor — the user's own project has custom
   middleware / auth / etc. we can't safely rewrite around. Writes a
   SHELLDOCS_SETUP.md with copy-paste snippets instead.

   Both modes are idempotent. */
internal static class InitCommand
{
    // Bump with <Version> in Directory.Build.props on every release. Determines
    // which ShellDocs.* versions the scaffold references. If stale, consumers
    // scaffolding via a new CLI get old packages that lack the fresh CLI's fixes.
    private const string ShellDocsVersion = "0.1.2-alpha";

    public static int Run(string? path, string dir, bool attach, bool yes, string theme)
    {
        if (attach) return AttachMode(dir, yes, theme);
        return CreateMode(path, dir, yes, theme);
    }

    // ---- CREATE MODE ----------------------------------------------------

    private static int CreateMode(string? explicitPath, string cwd, bool yes, string theme)
    {
        var cwdAbs = Path.GetFullPath(cwd);
        var outputAbs = string.IsNullOrEmpty(explicitPath)
            ? Path.Combine(cwdAbs, InferOutputPath(cwdAbs))
            : Path.GetFullPath(Path.Combine(cwdAbs, explicitPath));

        // Site name: strip the .Docs suffix we auto-append to the project
        // folder — "shell-tech.Docs" (project) → "shell-tech" (brand shown in header).
        var siteName = Path.GetFileName(outputAbs);
        if (siteName.EndsWith(".Docs", StringComparison.OrdinalIgnoreCase))
            siteName = siteName[..^5];
        if (string.IsNullOrEmpty(siteName)) siteName = "Docs";
        var githubRepo = yes ? "" : PromptGithub();

        AnsiConsole.WriteLine();
        var summary = new Table().Border(TableBorder.Rounded).AddColumn("").AddColumn("");
        summary.HideHeaders();
        summary.AddRow("[bold]Mode[/]",       "[yellow]create[/]");
        summary.AddRow("[bold]Output[/]",     $"[yellow]{outputAbs}[/]");
        summary.AddRow("[bold]Site name[/]",  $"[yellow]{siteName}[/]");
        summary.AddRow("[bold]Theme[/]",      $"[yellow]{theme}[/]");
        AnsiConsole.Write(summary);
        AnsiConsole.WriteLine();

        /* Refuse to overwrite an existing non-empty directory. If they want to
           re-scaffold on top of an existing dir, they can --attach it. */
        if (Directory.Exists(outputAbs) && Directory.EnumerateFileSystemEntries(outputAbs).Any())
        {
            AnsiConsole.MarkupLine($"[red]error:[/] target directory [yellow]{outputAbs}[/] already exists and isn't empty.");
            AnsiConsole.MarkupLine("[dim]cd into it and run [cyan]shelldocs init --attach[/dim][dim] to augment it in place.[/]");
            return 1;
        }

        // 1. dotnet new blazor
        AnsiConsole.MarkupLine("[dim]$[/] [cyan]dotnet new blazor -o " + outputAbs + "[/]");
        var newCode = RunDotnetNewBlazor(outputAbs);
        if (newCode != 0)
        {
            AnsiConsole.MarkupLine($"[red]error:[/] dotnet new blazor failed (exit {newCode})");
            return newCode;
        }

        // 2. Scaffold + patch the freshly-created project
        var csproj = FindCsproj(outputAbs);
        if (csproj is null)
        {
            AnsiConsole.MarkupLine("[red]error:[/] no .csproj found in fresh template — did dotnet new succeed?");
            return 1;
        }

        var changes = new List<string>();
        ScaffoldPackages(csproj, changes);
        ScaffoldContent(outputAbs, changes);
        ScaffoldDocsPage(outputAbs, changes);
        StripFreshTemplate(outputAbs, changes);
        PatchProgramCs(outputAbs, siteName, githubRepo, changes);
        PatchAppRazor(outputAbs, changes);
        RegisterWithSolution(csproj, cwdAbs, changes);

        // Report
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓[/] Wrote [bold]{changes.Count}[/] change(s):");
        foreach (var c in changes) AnsiConsole.MarkupLine($"  [dim]•[/] {c}");

        var rel = Path.GetRelativePath(cwdAbs, outputAbs);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Next:[/]");
        AnsiConsole.MarkupLine($"  [dim]$[/] [cyan]cd {rel}[/]");
        AnsiConsole.MarkupLine($"  [dim]$[/] [cyan]dotnet run[/]");
        AnsiConsole.MarkupLine("  [dim]→ visit[/] [cyan]/docs/introduction[/]");
        return 0;
    }

    /* Walks up from `startDir` looking for a .slnx (preferred) or .sln.
       Stops at the directory containing .git (repo root) or after 6 levels,
       whichever comes first. Returns null if no solution found. */
    internal static string? FindNearestSolution(string startDir)
    {
        var current = new DirectoryInfo(startDir);
        var depth = 0;
        while (current is not null && current.Exists && depth < 6)
        {
            var slnx = current.GetFiles("*.slnx").FirstOrDefault();
            if (slnx is not null) return slnx.FullName;
            var sln = current.GetFiles("*.sln").FirstOrDefault();
            if (sln is not null) return sln.FullName;
            // Don't walk past the git root.
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))) break;
            current = current.Parent;
            depth++;
        }
        return null;
    }

    private static void RegisterWithSolution(string csproj, string cwd, List<string> changes)
    {
        var sln = FindNearestSolution(cwd);
        if (sln is null) return;

        // Skip if the .slnx already lists this project (idempotent re-run guard).
        try
        {
            var xml = File.ReadAllText(sln);
            var csprojRel = Path.GetFileName(csproj);
            if (xml.Contains(csprojRel, StringComparison.OrdinalIgnoreCase))
            {
                // Already registered.
                return;
            }
        }
        catch { /* If we can't read, fall through and let dotnet sln handle it. */ }

        var psi = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(sln)!
        };
        psi.ArgumentList.Add("sln");
        psi.ArgumentList.Add(sln);
        psi.ArgumentList.Add("add");
        psi.ArgumentList.Add(csproj);

        using var proc = Process.Start(psi);
        if (proc is null) return;
        proc.WaitForExit();
        if (proc.ExitCode == 0)
        {
            changes.Add($"registered project in [cyan]{Path.GetFileName(sln)}[/]");
        }
    }

    private static int RunDotnetNewBlazor(string outputPath)
    {
        var psi = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        psi.ArgumentList.Add("new");
        psi.ArgumentList.Add("blazor");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outputPath);
        using var proc = Process.Start(psi);
        if (proc is null) return 1;
        proc.WaitForExit();
        return proc.ExitCode;
    }

    private static string InferOutputPath(string cwd)
    {
        var libName = Path.GetFileName(cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(libName)) libName = "Site";
        return Path.Combine("docs", libName + ".Docs");
    }

    // ---- ATTACH MODE ----------------------------------------------------

    private static int AttachMode(string dir, bool yes, string theme)
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
            AnsiConsole.MarkupLine("[dim]Run this inside a Blazor project root, or drop --attach to create a new project.[/]");
            return 1;
        }

        if (!IsBlazorProject(csproj))
        {
            AnsiConsole.MarkupLine($"[red]error:[/] [yellow]{Path.GetFileName(csproj)}[/] doesn't look like a Blazor project.");
            return 1;
        }

        var siteName = InferSiteName(csproj);
        var githubRepo = yes ? "" : PromptGithub();

        AnsiConsole.WriteLine();
        var summary = new Table().Border(TableBorder.Rounded).AddColumn("").AddColumn("");
        summary.HideHeaders();
        summary.AddRow("[bold]Mode[/]",       "[yellow]attach[/]");
        summary.AddRow("[bold]Project[/]",    $"[yellow]{Path.GetFileName(csproj)}[/]");
        summary.AddRow("[bold]Site name[/]",  $"[yellow]{siteName}[/]");
        summary.AddRow("[bold]Theme[/]",      $"[yellow]{theme}[/]");
        AnsiConsole.Write(summary);
        AnsiConsole.WriteLine();

        var changes = new List<string>();
        ScaffoldPackages(csproj, changes);
        ScaffoldContent(root, changes);
        ScaffoldDocsPage(root, changes);
        WriteIfMissing(
            Path.Combine(root, "SHELLDOCS_SETUP.md"),
            ScaffoldTemplates.SetupInstructionsMd(siteName, githubRepo),
            changes);

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
        AnsiConsole.MarkupLine("[bold]Next:[/] follow the snippets in [yellow]SHELLDOCS_SETUP.md[/], then [cyan]dotnet run[/].");
        return 0;
    }

    // ---- SHARED SCAFFOLDING ---------------------------------------------

    private static void ScaffoldPackages(string csproj, List<string> changes)
    {
        AddPackageIfMissing(csproj, "ShellDocs.Components", ShellDocsVersion, changes);
        AddPackageIfMissing(csproj, "ShellDocs.Tokens",     ShellDocsVersion, changes);
        AddContentCopyIfMissing(csproj, changes);
    }

    // Adds a Content Update item for content markdown/meta.json so
    // `dotnet publish` copies the markdown corpus into the publish output.
    // Without this, ContentRoot resolves fine under `dotnet run` (source dir)
    // but the published site has no content to render.
    internal static void AddContentCopyIfMissing(string csproj, List<string> changes)
    {
        var xml = File.ReadAllText(csproj);
        if (new Regex(@"<Content\s+Update=""content/", RegexOptions.IgnoreCase).IsMatch(xml))
            return;

        var closing = xml.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
        if (closing < 0) return;

        var block =
            $"  <ItemGroup>{Environment.NewLine}" +
            $"    <Content Update=\"content/**/*.md;content/**/meta.json\" CopyToOutputDirectory=\"PreserveNewest\" />{Environment.NewLine}" +
            $"  </ItemGroup>{Environment.NewLine}{Environment.NewLine}";

        File.WriteAllText(csproj, xml.Insert(closing, block));
        changes.Add($"added [cyan]content copy-to-output[/] to {Path.GetFileName(csproj)}");
    }

    private static void ScaffoldContent(string root, List<string> changes)
    {
        var contentDir = Path.Combine(root, "content", "docs");
        Directory.CreateDirectory(contentDir);
        WriteIfMissing(Path.Combine(contentDir, "introduction.md"), ScaffoldTemplates.IntroductionMd, changes);
        WriteIfMissing(Path.Combine(contentDir, "meta.json"),       ScaffoldTemplates.MetaJson, changes);
    }

    private static void ScaffoldDocsPage(string root, List<string> changes)
    {
        var pagesDir = LocateOrCreatePagesDir(root);
        WriteIfMissing(Path.Combine(pagesDir, "DocsPage.razor"), ScaffoldTemplates.DocsPageRazor, changes);
    }

    /* Strips the demo pages / NavMenu / MainLayout that `dotnet new blazor`
       ships, then writes a fumadocs-style welcome Home.razor rooted on
       HomeLayout. Called from CreateMode only — attach-mode leaves the
       user's existing pages alone. */
    internal static void StripFreshTemplate(string projectRoot, List<string> changes)
    {
        var componentsDir = Path.Combine(projectRoot, "Components");
        if (!Directory.Exists(componentsDir)) return;

        // Delete demo pages Counter, Weather. Home gets replaced below.
        var demoPages = new[] { "Counter.razor", "Weather.razor" };
        foreach (var p in demoPages)
        {
            var path = Path.Combine(componentsDir, "Pages", p);
            if (File.Exists(path))
            {
                File.Delete(path);
                changes.Add($"deleted [cyan]Components/Pages/{p}[/]");
            }
        }

        // Delete NavMenu (the purple sidebar) — MainLayout still references it,
        // so we also overwrite MainLayout as a bare pass-through afterward.
        var navMenus = new[] { "NavMenu.razor", "NavMenu.razor.css" };
        foreach (var n in navMenus)
        {
            var path = Path.Combine(componentsDir, "Layout", n);
            if (File.Exists(path))
            {
                File.Delete(path);
                changes.Add($"deleted [cyan]Components/Layout/{n}[/]");
            }
        }

        // Bare-passthrough MainLayout so Routes.razor still resolves it, but
        // every actual page uses @layout HomeLayout / DocsLayout to override.
        var mainLayoutPath = Path.Combine(componentsDir, "Layout", "MainLayout.razor");
        if (File.Exists(mainLayoutPath))
        {
            File.WriteAllText(mainLayoutPath, ScaffoldTemplates.BareMainLayoutRazor);
            changes.Add("simplified [cyan]Components/Layout/MainLayout.razor[/]");
        }
        var mainLayoutCss = Path.Combine(componentsDir, "Layout", "MainLayout.razor.css");
        if (File.Exists(mainLayoutCss))
        {
            File.Delete(mainLayoutCss);
            changes.Add("deleted [cyan]Components/Layout/MainLayout.razor.css[/]");
        }

        // Overwrite Home.razor with the welcome page.
        var homePath = Path.Combine(componentsDir, "Pages", "Home.razor");
        Directory.CreateDirectory(Path.GetDirectoryName(homePath)!);
        File.WriteAllText(homePath, ScaffoldTemplates.WelcomeHomeRazor);
        changes.Add("wrote [cyan]Components/Pages/Home.razor[/] (welcome page)");

        // The css file that ships alongside Home.razor is no longer relevant.
        var homeCss = Path.Combine(componentsDir, "Pages", "Home.razor.css");
        if (File.Exists(homeCss))
        {
            File.Delete(homeCss);
            changes.Add("deleted [cyan]Components/Pages/Home.razor.css[/]");
        }
    }

    // ---- CREATE-MODE PATCHERS -------------------------------------------

    /* Patches Program.cs of a freshly-created `dotnet new blazor` project.
       We know the exact template shape so anchor-based text insertion is safe. */
    internal static void PatchProgramCs(string projectRoot, string siteName, string githubRepo, List<string> changes)
    {
        var path = Path.Combine(projectRoot, "Program.cs");
        if (!File.Exists(path)) return;

        var src = File.ReadAllText(path);
        var original = src;

        // 1. `using ShellDocs.Components;` — after the existing using line.
        if (!src.Contains("using ShellDocs.Components;"))
        {
            var m = Regex.Match(src, @"^using\s+[^;]+;\s*$", RegexOptions.Multiline);
            if (m.Success)
            {
                src = src.Insert(m.Index + m.Length, Environment.NewLine + ScaffoldTemplates.ProgramUsing);
            }
            else
            {
                src = ScaffoldTemplates.ProgramUsing + Environment.NewLine + src;
            }
        }

        // 2. `builder.WebHost.UseStaticWebAssets();` — right after WebApplication.CreateBuilder.
        if (!src.Contains("UseStaticWebAssets"))
        {
            var m = Regex.Match(src, @"var\s+builder\s*=\s*WebApplication\.CreateBuilder\(args\)\s*;");
            if (m.Success)
            {
                var insertAt = m.Index + m.Length;
                src = src.Insert(insertAt, Environment.NewLine + Environment.NewLine + ScaffoldTemplates.ProgramWebHost);
            }
        }

        // 3. `builder.Services.AddShellDocs(...);` — after AddRazorComponents block.
        if (!src.Contains("AddShellDocs"))
        {
            var m = Regex.Match(src, @"builder\.Services\.AddRazorComponents\(\)[\s\S]*?;");
            if (m.Success)
            {
                var insertAt = m.Index + m.Length;
                src = src.Insert(insertAt,
                    Environment.NewLine + Environment.NewLine + ScaffoldTemplates.ProgramAddShellDocs(siteName, githubRepo));
            }
        }

        if (src != original)
        {
            File.WriteAllText(path, src);
            changes.Add("patched [cyan]Program.cs[/]");
        }
    }

    /* Patches App.razor of a freshly-created `dotnet new blazor` project. */
    internal static void PatchAppRazor(string projectRoot, List<string> changes)
    {
        // Fresh Blazor Web App puts App.razor under Components/.
        var path = Path.Combine(projectRoot, "Components", "App.razor");
        if (!File.Exists(path)) return;

        var src = File.ReadAllText(path);
        var original = src;

        // 1. Token + component CSS links — before <HeadOutlet />.
        if (!src.Contains("_content/ShellDocs.Tokens/tokens.css"))
        {
            var m = Regex.Match(src, @"<HeadOutlet\s*/?>", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                src = src.Insert(m.Index,
                    ScaffoldTemplates.AppTokenLinks + Environment.NewLine + "    ");
            }
        }

        // 2. Theme bootstrap script — also before <HeadOutlet />, after the CSS links.
        if (!src.Contains("shelldocs-theme'"))
        {
            var m = Regex.Match(src, @"<HeadOutlet\s*/?>", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                src = src.Insert(m.Index,
                    ScaffoldTemplates.AppThemeBootstrap + Environment.NewLine + "    ");
            }
        }

        // 3. shelldocs.js + Shiki module — after blazor.web.js. Fresh templates
        // in .NET 10 wrap the src in @Assets[...], so accept both variants.
        if (!src.Contains("_content/ShellDocs.Components/shelldocs.js"))
        {
            var m = Regex.Match(src,
                @"<script\s+src\s*=\s*""(?:@Assets\[""_framework/blazor\.web\.js""\]|_framework/blazor\.web\.js)""\s*></script>");
            if (m.Success)
            {
                var insertAt = m.Index + m.Length;
                src = src.Insert(insertAt,
                    Environment.NewLine + "    " + ScaffoldTemplates.AppScripts);
            }
        }

        // 4. <Routes /> needs @rendermode="InteractiveServer" or the theme
        // toggle (and every other Blazor interactive component) is dead.
        if (!Regex.IsMatch(src, @"<Routes\s+@rendermode"))
        {
            src = Regex.Replace(src, @"<Routes\s*/>", "<Routes @rendermode=\"InteractiveServer\" />");
        }

        // 5. Strip Bootstrap CSS + the fresh template's app.css. Both conflict
        // with the token system — Bootstrap paints inline <code> pink.
        src = Regex.Replace(src,
            @"\s*<link\s+rel=""stylesheet""\s+href=""@Assets\[""lib/bootstrap/dist/css/bootstrap\.min\.css""\]""\s*/>",
            "");
        src = Regex.Replace(src,
            @"\s*<link\s+rel=""stylesheet""\s+href=""@Assets\[""app\.css""\]""\s*/>",
            "");

        if (src != original)
        {
            File.WriteAllText(path, src);
            changes.Add("patched [cyan]Components/App.razor[/]");
        }
    }

    // ---- SHARED HELPERS -------------------------------------------------

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

    internal static void AddPackageIfMissing(string csproj, string package, string version, List<string> changes)
    {
        var xml = File.ReadAllText(csproj);
        if (new Regex($@"<PackageReference\s+Include=""{Regex.Escape(package)}""", RegexOptions.IgnoreCase).IsMatch(xml))
            return;

        var reference = $"    <PackageReference Include=\"{package}\" Version=\"{version}\" />";
        var itemGroup = Regex.Match(xml, @"<ItemGroup>\s*(?=\s*<PackageReference)", RegexOptions.IgnoreCase);
        string patched;
        if (itemGroup.Success)
        {
            patched = xml.Insert(itemGroup.Index + itemGroup.Length, reference + Environment.NewLine + "  ");
        }
        else
        {
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
        var candidates = new[]
        {
            Path.Combine(root, "Components", "Pages"),
            Path.Combine(root, "Pages"),
        };
        foreach (var c in candidates)
        {
            if (Directory.Exists(c)) return c;
        }
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
        catch { return ""; }
    }
}
