using System.Net;
using System.Text;

namespace ShellDocs.Markdown;

/* Parses a razor:preview fence into ordered nodes so every top-level sibling
   renders. Registered capitalised tags become component nodes (children kept
   raw for SlotRenderer); @code/@functions blocks, razor comments and directive
   lines are skipped since they can't render statically. */
internal sealed class PreviewParser
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr"
    };

    private static readonly string[] LineDirectives = ["@using", "@inject", "@page", "@attribute", "@namespace", "@implements", "@inherits", "@layout", "@rendermode", "@typeparam"];
    private static readonly string[] BlockDirectives = ["@code", "@functions"];

    private readonly TypeRegistry _registry;
    private readonly List<string> _warnings;

    public PreviewParser(TypeRegistry registry, List<string> warnings)
    {
        _registry = registry;
        _warnings = warnings;
    }

    public PreviewComponentNode? FirstComponent { get; private set; }
    // First capitalised tag that didn't resolve (+ its attributes / inner markup).
    public RazorTag? FirstUnknown { get; private set; }
    public string? FirstUnknownChildRaw { get; private set; }

    public IReadOnlyList<PreviewNode> Parse(string code)
    {
        var i = 0;
        return ParseNodes(code, ref i, stopTag: null);
    }

    private List<PreviewNode> ParseNodes(string text, ref int i, string? stopTag)
    {
        var nodes = new List<PreviewNode>();
        var sb = new StringBuilder();

        while (i < text.Length)
        {
            var c = text[i];

            if (c == '<')
            {
                if (string.CompareOrdinal(text, i, "<!--", 0, 4) == 0)
                {
                    Flush(nodes, sb);
                    var end = text.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    i = end < 0 ? text.Length : end + 3;
                    continue;
                }

                if (RazorTagScanner.TryRead(text, i, out var tag))
                {
                    Flush(nodes, sb);
                    if (tag.IsClose)
                    {
                        i = tag.End;
                        if (stopTag is not null && string.Equals(tag.Name, stopTag, StringComparison.OrdinalIgnoreCase))
                            return nodes;
                        continue; // stray close tag — drop it
                    }

                    if (char.IsUpper(tag.Name[0]))
                    {
                        var type = _registry.Resolve(tag.Name);
                        if (type is not null)
                        {
                            nodes.Add(ReadComponent(text, tag, type, ref i));
                            continue;
                        }
                        RecordUnknown(text, tag);
                    }

                    i = tag.End;
                    var children = tag.IsSelfClosing || VoidElements.Contains(tag.Name)
                        ? new List<PreviewNode>()
                        : ParseNodes(text, ref i, tag.Name);
                    var attrs = tag.Attributes.Where(a => !a.Key.StartsWith('@'))
                        .Select(a => new KeyValuePair<string, string?>(a.Key, a.Value is null ? null : WebUtility.HtmlDecode(a.Value)))
                        .ToList();
                    nodes.Add(new PreviewElementNode(tag.Name, attrs, children));
                    continue;
                }
            }
            else if (c == '@' && TrySkipRazorConstruct(text, ref i))
            {
                continue;
            }

            sb.Append(c);
            i++;
        }

        Flush(nodes, sb);
        return nodes;
    }

    private PreviewComponentNode ReadComponent(string text, RazorTag tag, Type type, ref int i)
    {
        string? childRaw = null;
        i = tag.End;
        if (!tag.IsSelfClosing)
        {
            var (closeStart, closeEnd) = RazorTagScanner.FindMatchingClose(text, tag.Name, tag.End, StringComparison.Ordinal);
            if (closeStart < 0)
            {
                _warnings.Add($"razor:preview: unclosed <{tag.Name}> — rendered without child content.");
            }
            else
            {
                childRaw = text[tag.End..closeStart];
                i = closeEnd;
            }
        }

        var node = new PreviewComponentNode(tag.Name, type, ToParameters(tag.Attributes), childRaw);
        FirstComponent ??= node;
        return node;
    }

    private void RecordUnknown(string text, RazorTag tag)
    {
        _warnings.Add($"razor:preview references unknown component <{tag.Name}>.");
        if (FirstUnknown is not null) return;
        FirstUnknown = tag;
        if (!tag.IsSelfClosing)
        {
            var (closeStart, _) = RazorTagScanner.FindMatchingClose(text, tag.Name, tag.End, StringComparison.Ordinal);
            if (closeStart >= 0) FirstUnknownChildRaw = text[tag.End..closeStart];
        }
    }

    // Component parameters: full attribute names kept (`@bind-Value`, `@ref`)
    // so SlotRenderer can skip them explicitly; bare attributes map to "".
    internal static IReadOnlyDictionary<string, string> ToParameters(IEnumerable<KeyValuePair<string, string?>> attrs)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in attrs) dict[k] = v ?? "";
        return dict;
    }

    private static bool TrySkipRazorConstruct(string text, ref int i)
    {
        // @* razor comment *@
        if (i + 1 < text.Length && text[i + 1] == '*')
        {
            var end = text.IndexOf("*@", i + 2, StringComparison.Ordinal);
            i = end < 0 ? text.Length : end + 2;
            return true;
        }

        foreach (var kw in BlockDirectives)
        {
            if (!StartsWithKeyword(text, i, kw)) continue;
            var brace = text.IndexOf('{', i + kw.Length);
            if (brace < 0 || !string.IsNullOrWhiteSpace(text[(i + kw.Length)..brace])) return false;
            var end = RazorTagScanner.SkipBalanced(text, brace, '{', '}');
            i = end < 0 ? text.Length : end;
            return true;
        }

        if (!AtLineStart(text, i)) return false;
        foreach (var kw in LineDirectives)
        {
            if (!StartsWithKeyword(text, i, kw)) continue;
            var nl = text.IndexOf('\n', i);
            i = nl < 0 ? text.Length : nl + 1;
            return true;
        }
        return false;
    }

    private static bool StartsWithKeyword(string text, int i, string kw)
        => string.CompareOrdinal(text, i, kw, 0, kw.Length) == 0
           && (i + kw.Length >= text.Length || !char.IsLetterOrDigit(text[i + kw.Length]));

    private static bool AtLineStart(string text, int i)
    {
        for (var j = i - 1; j >= 0; j--)
        {
            if (text[j] == '\n') return true;
            if (text[j] is not (' ' or '\t')) return false;
        }
        return true;
    }

    private static void Flush(List<PreviewNode> nodes, StringBuilder sb)
    {
        if (sb.Length == 0) return;
        nodes.Add(new PreviewTextNode(WebUtility.HtmlDecode(sb.ToString())));
        sb.Clear();
    }
}
