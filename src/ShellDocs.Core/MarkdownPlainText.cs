using System.Text.RegularExpressions;

namespace ShellDocs.Core;

/* Extracts plain text from markdown for search-body indexing. Strips YAML
   frontmatter, fenced code blocks, razor component tags, inline HTML, and
   the surface markdown syntax (headings, emphasis, links, images). Preserves
   the actual prose so token matching finds body-only hits. */
public static class MarkdownPlainText
{
    private static readonly Regex Frontmatter = new(@"^---\s*\r?\n[\s\S]*?\r?\n---\s*\r?\n", RegexOptions.Compiled);
    private static readonly Regex FencedBlock = new(@"^```[\s\S]*?^```", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex HtmlTag     = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Image       = new(@"!\[([^\]]*)\]\([^\)]*\)", RegexOptions.Compiled);
    private static readonly Regex Link        = new(@"\[([^\]]+)\]\([^\)]*\)", RegexOptions.Compiled);
    private static readonly Regex InlineCode  = new(@"`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex Emphasis    = new(@"(\*\*|__|\*|_)(.+?)\1", RegexOptions.Compiled);
    private static readonly Regex HeadingHash = new(@"^#{1,6}\s+", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ListMarker  = new(@"^\s*[-*+]\s+|^\s*\d+\.\s+", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex Blockquote  = new(@"^>\s?", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    public static string Extract(string markdown, int maxLength = 8000)
    {
        if (string.IsNullOrEmpty(markdown)) return "";

        var text = Frontmatter.Replace(markdown, "");
        text = FencedBlock.Replace(text, " ");
        text = HtmlTag.Replace(text, " ");
        text = Image.Replace(text, "$1");
        text = Link.Replace(text, "$1");
        text = InlineCode.Replace(text, "$1");
        text = Emphasis.Replace(text, "$2");
        text = HeadingHash.Replace(text, "");
        text = ListMarker.Replace(text, "");
        text = Blockquote.Replace(text, "");
        text = WhitespaceRun.Replace(text, " ").Trim();

        return text.Length > maxLength ? text[..maxLength] : text;
    }
}
