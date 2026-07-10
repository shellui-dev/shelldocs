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

    private static readonly Regex Attribute = new(
        @"(?<name>[A-Za-z][A-Za-z0-9]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled);

    public SlotExtractor(TypeRegistry registry)
    {
        _registry = registry;
    }

    public (string Processed, IReadOnlyList<Slot> Slots, IReadOnlyList<string> Warnings) Process(string markdown)
    {
        var slots = new List<Slot>();
        var warnings = new List<string>();

        // Mask code fences so component-tag scanning skips them.
        // razor:preview fences get replaced with a placeholder slot marker in-line;
        // other fences get a mask token that's restored verbatim at the end.
        var maskedFences = new Dictionary<string, string>();
        var processed = FenceBlock.Replace(markdown, m =>
        {
            var indent = m.Groups["indent"].Value;
            var lang = m.Groups["lang"].Value.Trim();
            var body = m.Groups["body"].Value;

            if (lang == "razor:preview" || lang.StartsWith("razor:preview "))
            {
                var preview = TryBuildPreviewSlot(body, warnings);
                if (preview is not null)
                {
                    slots.Add(preview);
                    return indent + PlaceholderHtml("preview", preview.Id);
                }
            }

            var maskId = NewMaskId();
            maskedFences[maskId] = m.Value;
            return maskId;
        });

        processed = ReplaceComponentTags(processed, slots, warnings);

        foreach (var (id, original) in maskedFences)
        {
            processed = processed.Replace(id, original);
        }

        // Sort slots by the position of their placeholder in the processed text
        // so the returned list reflects document order.
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

            var attrs = ParseAttributes(open.Groups["attrs"].Value);
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
                childRaw = text.Substring(open.Index + open.Length, closeStart - (open.Index + open.Length)).Trim();
                endIndex = closeEnd;
            }

            var slot = new ComponentSlot(NewSlotId(), registered, attrs, childRaw);
            slots.Add(slot);
            result.Append(PlaceholderHtml("component", slot.Id));
            cursor = endIndex;
        }

        return result.ToString();
    }

    private PreviewSlot? TryBuildPreviewSlot(string code, List<string> warnings)
    {
        var open = OpeningTag.Match(code);
        if (!open.Success)
        {
            warnings.Add("razor:preview fence must start with a component tag.");
            return null;
        }

        var name = open.Groups["name"].Value;
        var type = _registry.Resolve(name);
        if (type is null)
        {
            warnings.Add($"razor:preview references unknown component <{name}>.");
            return null;
        }

        var attrs = ParseAttributes(open.Groups["attrs"].Value);
        return new PreviewSlot(NewSlotId(), type, attrs, code, "razor");
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

    private static IReadOnlyDictionary<string, string> ParseAttributes(string attrsText)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Attribute.Matches(attrsText ?? ""))
        {
            result[m.Groups["name"].Value] = m.Groups["value"].Value;
        }
        return result;
    }

    private static string PlaceholderHtml(string kind, string id) =>
        $"<div data-shelldocs-slot=\"{kind}\" data-shelldocs-id=\"{id}\"></div>";

    private static string NewSlotId() => "s" + Guid.NewGuid().ToString("N")[..12];
    private static string NewMaskId() => "SHELLDOCS_MASK_" + Guid.NewGuid().ToString("N")[..12];
}
