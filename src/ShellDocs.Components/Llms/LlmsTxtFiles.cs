using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using ShellDocs.Core;
using ShellDocs.Markdown;

namespace ShellDocs.Components.Llms;

internal sealed record LlmsFile(string Path, string Body, bool IsDownload);

/* The llms.txt files (https://llmstxt.org) EnableLlmsTxt serves:
     /llms.txt, /llms-full.txt                   index and full text of the pages outside every
                                                 version plus the latest version's (all pages
                                                 when the site has no versions)
     {version root}/llms.txt, /llms-full.txt     the same for one version
     {page URL}.md                               one page as Markdown
     /agents/{file}, {version root}/agents/...   AGENTS.md, CLAUDE.md, copilot-instructions.md and
                                                 a Cursor rule, each a "## {SiteName}" section
   Links are absolute from `baseUrl` (an origin, an origin and path, or "" for root-relative). */
internal sealed class LlmsTxtFiles
{
    public const string ManifestPath = "/_shelldocs/llms.json";

    private readonly ShellDocsOptions _options;
    private readonly NavigationGraph _graph;
    private readonly TypeRegistry _registry;
    private readonly DocsVersionResolver _versions;
    private readonly string _contentRoot;

    public LlmsTxtFiles(ShellDocsOptions options, NavigationGraph graph, TypeRegistry registry,
        DocsVersionResolver versions, IWebHostEnvironment? env = null)
    {
        _options = options;
        _graph = graph;
        _registry = registry;
        _versions = versions;
        _contentRoot = env?.ContentRootPath ?? Directory.GetCurrentDirectory();
    }

    private LlmsTxtOptions Llms => _options.LlmsTxt ?? new LlmsTxtOptions();
    private string SiteName => string.IsNullOrWhiteSpace(_options.SiteName) ? "Documentation" : _options.SiteName.Trim();

    public IReadOnlyList<string> AgentFileNames =>
        ["AGENTS.md", "CLAUDE.md", "copilot-instructions.md", CursorRuleName];

    private string CursorRuleName
    {
        get
        {
            var slug = Regex.Replace(SiteName.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            return (slug.Length == 0 ? "docs" : slug) + ".mdc";
        }
    }

    // A version's files live under its root; null is the latest / unversioned scope at "/".
    private IEnumerable<(string Root, DocsVersion? Version)> Scopes()
    {
        yield return ("", null);
        foreach (var v in _versions.Versions) yield return (UrlPath.Normalize(v.RootUrl).TrimEnd('/'), v);
    }

    // Every path Get answers, for `shelldocs build`.
    public IEnumerable<string> AllPaths()
    {
        foreach (var (root, _) in Scopes())
        {
            yield return root + "/llms.txt";
            yield return root + "/llms-full.txt";
            if (!Llms.AgentFiles) continue;
            foreach (var name in AgentFileNames) yield return root + "/agents/" + name;
        }
        foreach (var url in _graph.AllUrls.Order(StringComparer.Ordinal))
            if (IsMarkdownPage(_graph.ResolveByUrl(url))) yield return UrlPath.Normalize(url) + ".md";
    }

    public LlmsFile? Get(string path, string baseUrl)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith('/')) return null;
        foreach (var (root, version) in Scopes())
        {
            if (Is(path, root + "/llms.txt")) return new(path, Index(root, version, baseUrl), false);
            if (Is(path, root + "/llms-full.txt")) return new(path, Full(root, version, baseUrl), false);
            if (!Llms.AgentFiles || !path.StartsWith(root + "/agents/", StringComparison.OrdinalIgnoreCase)) continue;
            var name = AgentFileNames.FirstOrDefault(n => Is(path, root + "/agents/" + n));
            if (name is not null) return new(path, AgentFile(root, version, baseUrl, name), true);
        }
        if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && _graph.ResolveByUrl(path[..^3]) is { } node && IsMarkdownPage(node))
            return new(path, Page(node, Converter(baseUrl)), false);
        return null;
    }

    private static bool Is(string path, string candidate) => string.Equals(path, candidate, StringComparison.OrdinalIgnoreCase);

    private static bool IsMarkdownPage(NavigationNode? node) =>
        node is { Kind: NodeKind.Page, Path: not null } && !string.IsNullOrEmpty(node.Url) && File.Exists(node.Path);

    private LlmsMarkdown Converter(string baseUrl) => new(_registry, _graph, _options.DemoSourceRoot, baseUrl);

    private List<NavigationNode> Pages(DocsVersion? version)
    {
        var latest = _versions.Versions.FirstOrDefault(v => v.IsLatest) ?? _versions.Versions.FirstOrDefault();
        return _graph.Flatten().Where(n => IsMarkdownPage(n) && (version is null
            ? _versions.FindContaining(n.Url) is not { } owner || owner == latest
            : _versions.FindContaining(n.Url) == version)).ToList();
    }

    private string Header(string root, DocsVersion? version, LlmsMarkdown md)
    {
        var sb = new StringBuilder($"# {SiteName}\n\n");
        var summary = OneLine(Llms.Summary ?? _options.SiteTagline);
        if (summary.Length > 0 && !".!?".Contains(summary[^1])) summary += ".";
        var shown = version ?? _versions.Versions.FirstOrDefault(v => v.IsLatest);
        if (shown is not null)
        {
            var others = _versions.Versions.Where(v => v != shown)
                .Select(v => $"[{v.Label}]({md.Url(UrlPath.Normalize(v.RootUrl).TrimEnd('/') + "/llms.txt", markdown: false)})").ToList();
            summary = $"{summary}{(summary.Length > 0 ? " " : "")}These docs are for {shown.Label}."
                + (others.Count > 0 ? " Other versions: " + string.Join(", ", others) + "." : "");
        }
        if (summary.Length > 0) sb.Append($"> {summary}\n\n");
        if (Rules() is { Length: > 0 } rules) sb.Append(rules).Append("\n\n");
        return sb.ToString();
    }

    private string Index(string root, DocsVersion? version, string baseUrl)
    {
        var md = Converter(baseUrl);
        var sb = new StringBuilder(Header(root, version, md));
        sb.Append($"The [full text]({md.Url(root + "/llms-full.txt", markdown: false)}) has every page below in one file.\n");

        var pages = Pages(version);
        var top = CommonAncestor(pages);
        var install = InstallPattern();
        // Pages outside every version listed with the latest one: its groups carry its label.
        var mixed = pages.Any(p => _versions.FindContaining(p.Url) is null) && pages.Any(p => _versions.FindContaining(p.Url) is not null);
        // One heading per group, in first-seen order: top-level pages between folders stay together.
        var groups = new List<(string Name, List<NavigationNode> Pages)>();
        foreach (var page in pages)
        {
            var g = Group(page, top);
            if (mixed && _versions.FindContaining(page.Url) is { } owner) g = g == "Pages" ? owner.Label : $"{owner.Label} / {g}";
            var existing = groups.FindIndex(x => x.Name == g);
            if (existing < 0) groups.Add((g, [page]));
            else groups[existing].Pages.Add(page);
        }
        foreach (var (name, groupPages) in groups)
        {
            sb.Append($"\n## {name}\n\n");
            foreach (var page in groupPages)
            {
                sb.Append($"- [{page.Title}]({md.Url(page.Url)})");
                if (OneLine(page.Description).TrimEnd('.') is { Length: > 0 } d) sb.Append(": ").Append(d).Append('.');
                if (install is not null && InstallCommand(page, install) is { } cmd) sb.Append($" Install: `{cmd}`");
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    private string Full(string root, DocsVersion? version, string baseUrl)
    {
        var md = Converter(baseUrl);
        var sb = new StringBuilder(Header(root, version, md).TrimEnd('\n'));
        foreach (var page in Pages(version)) sb.Append("\n\n---\n\n").Append(Page(page, md).TrimEnd('\n'));
        return sb.Append('\n').ToString();
    }

    private static string Page(NavigationNode page, LlmsMarkdown md)
    {
        var body = md.Convert(FrontmatterParser.Parse(File.ReadAllText(page.Path!)).Body);
        var h1 = $"# {page.Title}";
        if (body.StartsWith("# ", StringComparison.Ordinal))
        {
            var nl = body.IndexOf('\n');
            h1 = nl < 0 ? body : body[..nl];
            body = nl < 0 ? "" : body[(nl + 1)..].TrimStart('\n');
        }
        var sb = new StringBuilder(h1).Append("\n\n");
        if (OneLine(page.Description) is { Length: > 0 } d) sb.Append($"> {d}\n\n");
        sb.Append($"Source: {md.Url(page.Url, markdown: false)}\n\n");
        return sb.Append(body).Append('\n').ToString();
    }

    private string AgentFile(string root, DocsVersion? version, string baseUrl, string name)
    {
        var md = Converter(baseUrl);
        var shown = version ?? _versions.Versions.FirstOrDefault(v => v.IsLatest);
        var example = Pages(version).FirstOrDefault();
        var intro = $"This project uses {SiteName}{(shown is null ? "" : " " + shown.Label)}. Before writing code that uses it, "
            + $"read {md.Url(root + "/llms.txt", markdown: false)}: it lists every docs page"
            + (Llms.InstallCommandPattern is not null ? " with its install command" : "")
            + ". Every page is also available as Markdown at its URL plus `.md`"
            + (example is null ? "." : $", for example {md.Url(example.Url)}.");
        var section = $"## {SiteName}\n\n{intro}\n";
        if (Rules() is { Length: > 0 } rules) section += "\n" + DemoteHeadings(rules) + "\n";

        if (!name.EndsWith(".mdc", StringComparison.Ordinal)) return "\n" + section;
        var description = OneLine(Llms.Summary ?? _options.SiteTagline) is { Length: > 0 } s ? s : $"{SiteName} docs for AI agents";
        return $"---\ndescription: {description}\nalwaysApply: true\n---\n\n{section}";
    }

    private string? Rules()
    {
        if (string.IsNullOrWhiteSpace(Llms.RulesFile)) return null;
        var path = Path.IsPathRooted(Llms.RulesFile) ? Llms.RulesFile : Path.Combine(_contentRoot, Llms.RulesFile);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n").Trim() : null;
    }

    // The agent files are a "## Site" section, so the rules' headings move one level down.
    private static string DemoteHeadings(string markdown)
    {
        var inFence = false;
        return string.Join("\n", markdown.Split('\n').Select(line =>
        {
            var t = line.TrimStart();
            if (t.StartsWith("```") || t.StartsWith("~~~")) inFence = !inFence;
            return !inFence && t.StartsWith('#') ? "#" + t : line;
        }));
    }

    private Regex? InstallPattern() =>
        string.IsNullOrWhiteSpace(Llms.InstallCommandPattern) ? null : new Regex(Llms.InstallCommandPattern, RegexOptions.None, TimeSpan.FromSeconds(1));

    // First match in the page's code blocks.
    private static string? InstallCommand(NavigationNode page, Regex pattern)
    {
        foreach (Match fence in SlotExtractor.FenceBlock.Matches(File.ReadAllText(page.Path!).Replace("\r\n", "\n")))
            if (pattern.Match(fence.Groups["body"].Value) is { Success: true } m) return m.Value.Trim();
        return null;
    }

    /* The page's sidebar section below the scope's common folder ("Guides"); a section
       inside a folder adds the next one ("Components / Form") and deeper ones fold in. */
    private string Group(NavigationNode page, NavigationNode? top)
    {
        var sections = new List<NavigationNode>();
        for (var n = page.Parent; n is not null && n != top && n.Parent is not null; n = n.Parent)
            if (n.Kind == NodeKind.Section && !string.IsNullOrWhiteSpace(n.Title) && !_versions.IsVersionNode(n)) sections.Insert(0, n);
        if (sections.Count == 0) return "Pages";
        return sections.Count > 1 && sections[0].Path is not null ? $"{sections[0].Title} / {sections[1].Title}" : sections[0].Title;
    }

    private static NavigationNode? CommonAncestor(IReadOnlyList<NavigationNode> pages)
    {
        List<NavigationNode>? common = null;
        foreach (var page in pages)
        {
            var chain = new List<NavigationNode>();
            for (var n = page.Parent; n is not null; n = n.Parent) chain.Insert(0, n);
            if (common is null) { common = chain; continue; }
            var k = 0;
            while (k < common.Count && k < chain.Count && common[k] == chain[k]) k++;
            common = common[..k];
        }
        return common?.LastOrDefault();
    }

    private static string OneLine(string? s) => s is null ? "" : Regex.Replace(s, @"\s+", " ").Trim();
}
