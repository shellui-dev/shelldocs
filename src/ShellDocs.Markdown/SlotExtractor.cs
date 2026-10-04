using System.Text;
using System.Text.RegularExpressions;

namespace ShellDocs.Markdown;

internal class SlotExtractor
{
    private readonly TypeRegistry _registry;

    /* CommonMark fenced block: a run of three or more backticks or tildes, closed by
       a run of the same character at least as long. A longer fence can show a
       shorter one in its body, so the two must not be paired with each other. */
    private static readonly Regex FenceBlock = new(
        @"^(?<indent>[ \t]*)(?<fence>`{3,}|(?<tilde>~{3,}))(?<lang>(?(tilde)(?!~)[^\r\n]*|[^`\r\n]*))\r?\n" +
        @"(?:(?<body>[\s\S]*?)\r?\n)??\k<indent>\k<fence>(?(tilde)~*|`*)[ \t]*(?=\r?\n|$)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex BlankLine = new(@"\n[ \t]*\r?\n", RegexOptions.Compiled);

    /* CommonMark code span: a backtick run, content, and a closing run of the same
       length. May wrap lines but not cross a blank line. */
    private static readonly Regex InlineCode = new(
        @"(?<!`)(?<ticks>`+)(?!`)(?:[^`\r\n]|\r?\n(?![ \t]*\r?\n)|`+(?!`))*?(?<!`)\k<ticks>(?!`)",
        RegexOptions.Compiled);

    // Mask token → original text, restored at the end and inside component child markup.
    private readonly Dictionary<string, string> _masks = new();

    public SlotExtractor(TypeRegistry registry)
    {
        _registry = registry;
    }

    public (string Processed, IReadOnlyList<Slot> Slots, IReadOnlyList<string> Warnings) Process(string markdown)
    {
        var slots = new List<Slot>();
        var warnings = new List<string>();

        // Mask fences and code spans so tag scanning skips them; razor:preview fences
        // become slot placeholders, the rest are restored verbatim at the end.
        var processed = FenceBlock.Replace(markdown, m =>
        {
            var indent = m.Groups["indent"].Value;
            var lang = m.Groups["lang"].Value.Trim();
            var body = m.Groups["body"].Value;

            if (lang == "razor:preview" || lang.StartsWith("razor:preview "))
            {
                var preview = TryBuildPreviewSlot(body, warnings, PreviewLayout(lang));
                if (preview is not null)
                {
                    slots.Add(preview);
                    return indent + PlaceholderHtml("preview", preview.Id);
                }
            }

            return Mask(m.Value);
        });
        processed = InlineCode.Replace(processed, m => Mask(m.Value));

        processed = ReplaceComponentTags(processed, slots, warnings);
        processed = Unmask(processed);

        // Return slots in document order.
        var ordered = slots.OrderBy(s => processed.IndexOf(s.Id, StringComparison.Ordinal)).ToList();
        return (processed, ordered, warnings);
    }

    private string ReplaceComponentTags(string text, List<Slot> slots, List<string> warnings)
    {
        var result = new StringBuilder(text.Length);
        var cursor = 0;

        while (cursor < text.Length)
        {
            if (!TryFindOpeningTag(text, cursor, out var open))
            {
                result.Append(text, cursor, text.Length - cursor);
                break;
            }

            var name = open.Name;
            var registered = _registry.Resolve(name);

            if (registered is null)
            {
                warnings.Add($"Unknown component <{name}> — passed through as raw markup.");
                result.Append(text, cursor, open.End - cursor);
                cursor = open.End;
                continue;
            }

            result.Append(text, cursor, open.Start - cursor);

            var attrs = UnmaskValues(open.Attributes);
            string? childRaw = null;
            var endIndex = open.End;

            if (!open.IsSelfClosing)
            {
                var (closeStart, closeEnd) = RazorTagScanner.FindMatchingClose(text, name, open.End, StringComparison.Ordinal);
                if (closeStart < 0)
                {
                    warnings.Add($"Unclosed <{name}> — passed through as raw markup.");
                    result.Append(text, open.Start, open.End - open.Start);
                    cursor = open.End;
                    continue;
                }
                /* No Trim(): SlotRenderer.Dedent needs the first line's indent,
                   or Markdig reads the remaining lines as an indented code block. */
                childRaw = Unmask(text[open.End..closeStart]);
                endIndex = closeEnd;
            }

            var slot = new ComponentSlot(NewSlotId(), registered, attrs, childRaw);
            slots.Add(slot);
            result.Append(PlaceholderHtml("component", slot.Id));
            cursor = endIndex;
        }

        return result.ToString();
    }

    /* Next component-shaped opening tag at or after `from`: an uppercase
       alphanumeric name, read by the same scanner razor:preview uses so quoted
       values may contain '>' or text that looks like another attribute. */
    private static bool TryFindOpeningTag(string text, int from, out RazorTag tag)
    {
        for (var lt = text.IndexOf('<', from); lt >= 0; lt = text.IndexOf('<', lt + 1))
        {
            if (lt + 1 >= text.Length || text[lt + 1] is < 'A' or > 'Z') continue;
            if (!RazorTagScanner.TryRead(text, lt, out tag) || tag.IsClose) continue;
            if (!tag.Name.All(char.IsAsciiLetterOrDigit)) continue;
            // An unterminated quote would otherwise run on to the next quote on the page.
            if (BlankLine.Match(text, tag.Start, tag.End - tag.Start).Success) continue;
            return true;
        }
        tag = default;
        return false;
    }

    // `razor:preview stretch` lets block-level examples fill the frame; `scroll` lets wide ones scroll.
    private static string? PreviewLayout(string lang)
    {
        var words = lang["razor:preview".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Contains("stretch", StringComparer.OrdinalIgnoreCase)) return "stretch";
        if (words.Contains("scroll", StringComparer.OrdinalIgnoreCase)) return "scroll";
        return null;
    }

    private PreviewSlot? TryBuildPreviewSlot(string code, List<string> warnings, string? layout)
    {
        var parser = new PreviewParser(_registry, warnings);
        var nodes = parser.Parse(code);

        if (parser.FirstComponent is { } first)
        {
            return new PreviewSlot(NewSlotId(), first.ComponentType, first.Parameters, code, "razor",
                first.ChildContentRaw, Nodes: nodes, Layout: layout);
        }

        if (parser.FirstUnknown is { } unknown)
        {
            // Error slot → a visible panel, rather than silently rendering a code block.
            var name = unknown.Name;
            var msg = $"Unknown component <{name}>. Register it via `o.RegisterComponent<{name}>()` or `o.RegisterComponentsFromAssembly<TMarker>()`.";
            return new PreviewSlot(NewSlotId(), null, PreviewParser.ToParameters(unknown.Attributes), code, "razor",
                parser.FirstUnknownChildRaw, Error: msg);
        }

        warnings.Add("razor:preview fence must contain a component tag.");
        return null;
    }

    /* Attributes keep their full names (`@bind-Value`, not `Value`) so SlotRenderer
       can skip directive attributes. Code spans are restored per value, after the
       tag is parsed, so quotes inside a span can't split the value. */
    private IReadOnlyDictionary<string, string> UnmaskValues(IEnumerable<KeyValuePair<string, string?>> attrs)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in attrs) dict[name] = Unmask(value ?? "");
        return dict;
    }

    private static string PlaceholderHtml(string kind, string id) =>
        $"<div data-shelldocs-slot=\"{kind}\" data-shelldocs-id=\"{id}\"></div>";

    private static string NewSlotId() => "s" + Guid.NewGuid().ToString("N")[..12];
    private string Mask(string original)
    {
        var id = NewMaskId();
        _masks[id] = original;
        return id;
    }

    private string Unmask(string text)
    {
        if (_masks.Count == 0 || text.IndexOf("SHELLDOCS_MASK_", StringComparison.Ordinal) < 0) return text;
        foreach (var (id, original) in _masks) text = text.Replace(id, original);
        return text;
    }

    private static string NewMaskId() => "SHELLDOCS_MASK_" + Guid.NewGuid().ToString("N")[..12];
}
