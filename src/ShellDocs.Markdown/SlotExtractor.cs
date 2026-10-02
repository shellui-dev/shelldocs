using System.Text;
using System.Text.RegularExpressions;

namespace ShellDocs.Markdown;

internal class SlotExtractor
{
    private readonly TypeRegistry _registry;

    private static readonly Regex FenceBlock = new(
        @"^(?<indent>[ \t]*)```(?<lang>[^\n\r]*)\r?\n(?<body>[\s\S]*?)\r?\n\1```(?=\r?\n|$)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex OpeningTag = new(
        @"<(?<name>[A-Z][A-Za-z0-9]*)(?<attrs>\s[^>]*?)?\s*(?<self>/)?>",
        RegexOptions.Compiled);

    private static readonly Regex ClosingTag = new(
        @"</(?<name>[A-Z][A-Za-z0-9]*)\s*>",
        RegexOptions.Compiled);

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
            var open = OpeningTag.Match(text, cursor);
            if (!open.Success)
            {
                result.Append(text, cursor, text.Length - cursor);
                break;
            }

            var name = open.Groups["name"].Value;
            var isSelfClosing = open.Groups["self"].Success;
            var registered = _registry.Resolve(name);

            if (registered is null)
            {
                warnings.Add($"Unknown component <{name}> — passed through as raw markup.");
                result.Append(text, cursor, open.Index + open.Length - cursor);
                cursor = open.Index + open.Length;
                continue;
            }

            result.Append(text, cursor, open.Index - cursor);

            var attrs = ParseAttributes(Unmask(open.Groups["attrs"].Value));
            string? childRaw = null;
            int endIndex;

            if (isSelfClosing)
            {
                endIndex = open.Index + open.Length;
            }
            else
            {
                var (closeStart, closeEnd) = FindMatchingClose(text, name, open.Index + open.Length);
                if (closeStart < 0)
                {
                    warnings.Add($"Unclosed <{name}> — passed through as raw markup.");
                    result.Append(text, open.Index, open.Length);
                    cursor = open.Index + open.Length;
                    continue;
                }
                /* No Trim(): SlotRenderer.Dedent needs the first line's indent,
                   or Markdig reads the remaining lines as an indented code block. */
                childRaw = Unmask(text.Substring(open.Index + open.Length, closeStart - (open.Index + open.Length)));
                endIndex = closeEnd;
            }

            var slot = new ComponentSlot(NewSlotId(), registered, attrs, childRaw);
            slots.Add(slot);
            result.Append(PlaceholderHtml("component", slot.Id));
            cursor = endIndex;
        }

        return result.ToString();
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

    private static (int Start, int End) FindMatchingClose(string text, string name, int fromIndex)
    {
        var depth = 1;
        var searchFrom = fromIndex;
        while (depth > 0)
        {
            var open = FindNextTag(OpeningTag, text, name, searchFrom);
            var close = FindNextTag(ClosingTag, text, name, searchFrom);

            if (close is null) return (-1, -1);

            if (open is not null && open.Index < close.Index)
            {
                if (!open.Groups["self"].Success) depth++;
                searchFrom = open.Index + open.Length;
            }
            else
            {
                depth--;
                if (depth == 0) return (close.Index, close.Index + close.Length);
                searchFrom = close.Index + close.Length;
            }
        }
        return (-1, -1);
    }

    private static Match? FindNextTag(Regex regex, string text, string name, int fromIndex)
    {
        foreach (Match m in regex.Matches(text, fromIndex))
        {
            if (m.Groups["name"].Value == name) return m;
        }
        return null;
    }

    // Full names (`@bind-Value`, not `Value`) so SlotRenderer can skip directive attributes.
    private static IReadOnlyDictionary<string, string> ParseAttributes(string attrsText)
        => PreviewParser.ToParameters(RazorTagScanner.ParseAttributes(attrsText));

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
