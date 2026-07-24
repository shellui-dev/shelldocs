using System.Text.RegularExpressions;

namespace ShellDocs.Core;

/* An in-memory search index built from the navigation graph. Each entry
   represents one searchable thing — a page, or a heading within a page.
   Page entries carry a trimmed plain-text body so client-side match can
   find hits that aren't in the title/description surface. */
public sealed class SearchIndex
{
    public IReadOnlyList<SearchEntry> Entries { get; }

    public SearchIndex(IReadOnlyList<SearchEntry> entries) => Entries = entries;

    public static SearchIndex FromGraph(NavigationGraph graph)
    {
        var entries = new List<SearchEntry>();
        Walk(graph.Root, section: null, entries);
        return new SearchIndex(entries);
    }

    private static void Walk(NavigationNode node, string? section, List<SearchEntry> acc)
    {
        if (node.Kind == NodeKind.Page && !string.IsNullOrEmpty(node.Url))
        {
            acc.Add(new SearchEntry(
                Url: node.Url,
                Title: node.Title,
                Description: node.Description,
                Section: section,
                Kind: SearchEntryKind.Page,
                Body: ExtractBodyFromFile(node.Path)));

            // Prefer headings already extracted at render time; otherwise pull
            // them from the source markdown ourselves so the index isn't blank
            // at startup (headings normally populate only when a page renders).
            var headings = node.Headings.Count > 0
                ? node.Headings
                : ExtractHeadingsFromFile(node.Path);
            foreach (var h in headings.Where(h => h.Level == 2 || h.Level == 3))
            {
                acc.Add(new SearchEntry(
                    Url: node.Url + "#" + h.Id,
                    Title: h.Text,
                    Description: null,
                    Section: node.Title,
                    Kind: SearchEntryKind.Heading));
            }
        }

        var nextSection = node.Kind == NodeKind.Section && !string.IsNullOrEmpty(node.Title)
            ? node.Title
            : section;
        foreach (var child in node.Children)
        {
            Walk(child, nextSection, acc);
        }
    }

    private static readonly Regex FencedBlock = new(@"^```[\s\S]*?^```", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex HeadingLine = new(@"^(#{2,3})\s+(.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    private static IReadOnlyList<Heading> ExtractHeadingsFromFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Array.Empty<Heading>();
        var text = File.ReadAllText(path);
        // Strip fenced code blocks so # inside code doesn't parse as a heading.
        text = FencedBlock.Replace(text, "");
        var list = new List<Heading>();
        foreach (Match m in HeadingLine.Matches(text))
        {
            var level = m.Groups[1].Value.Length;
            var raw = m.Groups[2].Value.Trim();
            list.Add(new Heading(level, raw, Slugify(raw)));
        }
        return list;
    }

    private static string? ExtractBodyFromFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        return MarkdownPlainText.Extract(File.ReadAllText(path));
    }

    private static string Slugify(string text)
    {
        var lowered = text.ToLowerInvariant();
        var sb = new System.Text.StringBuilder(lowered.Length);
        var lastDash = false;
        foreach (var ch in lowered)
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); lastDash = false; }
            else if ((ch == ' ' || ch == '-' || ch == '_') && !lastDash)
            {
                sb.Append('-'); lastDash = true;
            }
        }
        return sb.ToString().Trim('-');
    }
}

public record SearchEntry(
    string Url,
    string Title,
    string? Description,
    string? Section,
    SearchEntryKind Kind,
    string? Body = null);

public enum SearchEntryKind { Page, Heading }
