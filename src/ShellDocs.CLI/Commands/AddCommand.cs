using System.Text.RegularExpressions;
using ShellDocs.Templates;
using Spectre.Console;

namespace ShellDocs.CLI.Commands;

/* `shelldocs add <template> <name>` — scaffolds a starter markdown page from a
   template into the project's content root. Templates:
     component — content/docs/components/<slug>.md   (razor:preview + TypeTable skeleton)
     guide     — content/docs/guides/<slug>.md       (Steps skeleton)
     page      — content/docs/<slug>.md              (blank frontmatter + title)

   Slugifies component-style names ("MyBigCard" → "my-big-card"), preserves
   kebab/snake input verbatim, and refuses to overwrite an existing file unless
   --force is passed. */
internal static class AddCommand
{
    public static int Run(string template, string name, string dir, bool force)
    {
        var templateKey = (template ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(name))
        {
            AnsiConsole.MarkupLine("[red]error:[/] name is required.");
            AnsiConsole.MarkupLine("[dim]usage: shelldocs add <component|guide|page> <name>[/]");
            return 1;
        }

        var contentRoot = ResolveContentRoot(dir);
        if (contentRoot is null)
        {
            AnsiConsole.MarkupLine($"[red]error:[/] no [yellow]content/[/] directory found under [yellow]{dir}[/].");
            AnsiConsole.MarkupLine("[dim]run this from your docs project root, or use --dir to point at it.[/]");
            return 1;
        }

        var (subDir, displayName, slug, body) = templateKey switch
        {
            "component"       => ("docs/components", DisplayName(name), Slugify(name), PageTemplates.ComponentPage(DisplayName(name))),
            "guide"           => ("docs/guides",     TitleCase(name),   Slugify(name), PageTemplates.GuidePage(TitleCase(name))),
            "page"            => ("docs",            TitleCase(name),   Slugify(name), PageTemplates.BlankPage(TitleCase(name))),
            _ => (null, "", "", "")!
        };

        if (subDir is null)
        {
            AnsiConsole.MarkupLine($"[red]error:[/] unknown template [yellow]{template}[/].");
            AnsiConsole.MarkupLine("[dim]expected one of: [cyan]component[/], [cyan]guide[/], [cyan]page[/].[/]");
            return 1;
        }

        var targetDir = Path.Combine(contentRoot, subDir);
        Directory.CreateDirectory(targetDir);
        var targetFile = Path.Combine(targetDir, slug + ".md");

        if (File.Exists(targetFile) && !force)
        {
            AnsiConsole.MarkupLine($"[red]error:[/] [yellow]{PrettyPath(targetFile)}[/] already exists. Pass [cyan]--force[/] to overwrite.");
            return 1;
        }

        File.WriteAllText(targetFile, body);
        AnsiConsole.MarkupLine($"[green]created[/] [cyan]{PrettyPath(targetFile)}[/]");
        AnsiConsole.MarkupLine($"[dim]edit the frontmatter + TODOs, then reload the dev server.[/]");
        return 0;
    }

    private static string? ResolveContentRoot(string dir)
    {
        var abs = Path.GetFullPath(dir);
        var candidate = Path.Combine(abs, "content");
        if (Directory.Exists(candidate)) return candidate;
        return null;
    }

    /* "MyBigCard" → "my-big-card"; "getting-started" → "getting-started";
       "Getting Started" → "getting-started"; drops non-alphanumeric except '-' */
    private static string Slugify(string raw)
    {
        var withDashes = Regex.Replace(raw.Trim(), @"(?<=[a-z0-9])(?=[A-Z])", "-");
        withDashes = Regex.Replace(withDashes, @"[\s_]+", "-");
        withDashes = Regex.Replace(withDashes, @"[^A-Za-z0-9\-]", "");
        withDashes = Regex.Replace(withDashes, @"-{2,}", "-").Trim('-');
        return withDashes.ToLowerInvariant();
    }

    /* Component name stays PascalCase for the display (matches how <Button> etc.
       are referenced in razor:preview). If input already contains spaces, keep
       the first-letter-uppercase form. */
    private static string DisplayName(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Contains(' ') || trimmed.Contains('-') || trimmed.Contains('_'))
            return TitleCase(trimmed);
        return char.IsUpper(trimmed[0]) ? trimmed : char.ToUpper(trimmed[0]) + trimmed[1..];
    }

    /* "getting-started" → "Getting Started"; "MyGuide" → "My Guide" */
    private static string TitleCase(string raw)
    {
        var spaced = Regex.Replace(raw.Trim(), @"[-_]+", " ");
        spaced = Regex.Replace(spaced, @"(?<=[a-z0-9])(?=[A-Z])", " ");
        var parts = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(p => char.ToUpper(p[0]) + p[1..].ToLowerInvariant()));
    }

    private static string PrettyPath(string abs)
    {
        var cwd = Directory.GetCurrentDirectory();
        return abs.StartsWith(cwd, StringComparison.OrdinalIgnoreCase)
            ? abs[(cwd.Length + 1)..].Replace('\\', '/')
            : abs.Replace('\\', '/');
    }
}
