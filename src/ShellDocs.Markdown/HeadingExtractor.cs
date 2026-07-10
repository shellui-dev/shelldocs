using System.Text;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using ShellDocs.Core;

namespace ShellDocs.Markdown;

internal static class HeadingExtractor
{
    private static readonly Regex NonAlphaNumericRun = new("[^a-z0-9]+", RegexOptions.Compiled);

    public static IReadOnlyList<Heading> Extract(MarkdownDocument document)
    {
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        var headings = new List<Heading>();

        foreach (var block in document.Descendants<HeadingBlock>())
        {
            var text = ExtractText(block.Inline).Trim();
            if (text.Length == 0) continue;

            var id = Slugify(text, used);
            headings.Add(new Heading(block.Level, text, id));
        }

        return headings;
    }

    private static string ExtractText(ContainerInline? inline)
    {
        if (inline is null) return "";
        var sb = new StringBuilder();
        Walk(inline, sb);
        return sb.ToString();
    }

    private static void Walk(Inline inline, StringBuilder sb)
    {
        switch (inline)
        {
            case LiteralInline lit:
                sb.Append(lit.Content.ToString());
                break;
            case CodeInline code:
                sb.Append(code.Content);
                break;
            case LineBreakInline:
                sb.Append(' ');
                break;
            case ContainerInline container:
                foreach (var child in container) Walk(child, sb);
                break;
        }
    }

    private static string Slugify(string text, Dictionary<string, int> used)
    {
        var lower = text.ToLowerInvariant();
        var slug = NonAlphaNumericRun.Replace(lower, "-").Trim('-');
        if (slug.Length == 0) slug = "section";

        if (used.TryGetValue(slug, out var count))
        {
            used[slug] = count + 1;
            return $"{slug}-{count + 1}";
        }
        used[slug] = 1;
        return slug;
    }
}
