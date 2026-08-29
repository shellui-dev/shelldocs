using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using ShellDocs.Markdown;

namespace ShellDocs.Components.Content;

/* Turns a slice of markdown (or raw HTML-with-component-tags) into a Blazor
   RenderFragment that renders nested components as real DynamicComponents,
   recursively — so <CardGrid><Card /><Card /></CardGrid> and friends work
   inside razor:preview blocks and inline ChildContent. */
internal static class SlotRenderer
{
    public static RenderFragment FromMarkup(MarkdownRenderer renderer, string raw) => builder =>
    {
        /* Markdig treats any block indented 4+ spaces as a code block, so
           children of <Steps>, <FileTree>, etc. authored with the outer tag's
           natural indent would render as literal <pre> instead of components.
           Strip the common leading whitespace before feeding to the renderer. */
        var doc = renderer.Render(Dedent(raw));
        var parts = SlotSplitter.Split(doc);
        var seq = 0;
        foreach (var part in parts)
        {
            if (part is HtmlPart html)
            {
                builder.AddMarkupContent(seq++, html.Html);
            }
            else if (part is SlotPart s && s.Slot is ComponentSlot comp)
            {
                Emit(builder, ref seq, renderer, comp);
            }
        }
    };

    private static void Emit(RenderTreeBuilder builder, ref int seq, MarkdownRenderer renderer, ComponentSlot slot)
    {
        builder.OpenComponent<DynamicComponent>(seq++);
        builder.AddAttribute(seq++, "Type", slot.ComponentType);
        builder.AddAttribute(seq++, "Parameters", BuildParameters(renderer, slot.ComponentType, slot.Parameters, slot.ChildContentRaw));
        builder.CloseComponent();
    }

    private static string Dedent(string raw)
    {
        var lines = raw.Split('\n');
        var minIndent = int.MaxValue;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var indent = 0;
            while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t')) indent++;
            if (indent < minIndent) minIndent = indent;
        }
        if (minIndent <= 0 || minIndent == int.MaxValue) return raw;
        var sb = new StringBuilder(raw.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length >= minIndent && !string.IsNullOrWhiteSpace(line))
                sb.Append(line.AsSpan(minIndent));
            else
                sb.Append(line);
            if (i < lines.Length - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    public static IDictionary<string, object> BuildParameters(
        MarkdownRenderer renderer,
        Type componentType,
        IReadOnlyDictionary<string, string> attrs,
        string? childContentRaw)
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        var props = GetParameterProps(componentType);
        foreach (var (k, v) in attrs)
        {
            dict[k] = props.TryGetValue(k, out var prop) ? Coerce(v, prop.PropertyType) : v;
        }
        if (!string.IsNullOrWhiteSpace(childContentRaw))
        {
            // Route direct-child tags whose name matches a RenderFragment
            // param (other than ChildContent) into that named slot. Any
            // remaining text becomes ChildContent. Lets authors write
            // <Alert><Icon><svg/></Icon>Body</Alert> and have <Icon>'s
            // inner content routed to Alert.Icon instead of being flattened
            // into the ChildContent stream.
            var slotNames = props
                .Where(kv => kv.Key != "ChildContent" && typeof(RenderFragment).IsAssignableFrom(kv.Value.PropertyType))
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.Ordinal);

            var remaining = childContentRaw;
            if (slotNames.Count > 0)
            {
                foreach (var slotName in slotNames)
                {
                    var extracted = ExtractNamedSlot(remaining, slotName);
                    if (extracted.Content is not null)
                    {
                        dict[slotName] = FromMarkup(renderer, extracted.Content);
                        remaining = extracted.Remaining;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(remaining))
                dict["ChildContent"] = FromMarkup(renderer, remaining);

            /* If the target declares a ChildContentSource [Parameter] (as
               ComponentPreview does for reconstructing its source view),
               pass the raw markup through unchanged in addition to the
               RenderFragments above. */
            if (props.ContainsKey("ChildContentSource"))
                dict["ChildContentSource"] = childContentRaw;
        }
        return dict;
    }

    // Finds `<TagName>...</TagName>` (balanced) or `<TagName />` in `text`
    // and returns its inner content + `text` with that occurrence removed.
    // Only the first occurrence is extracted; multi-instance named slots
    // aren't a common pattern.
    private static (string? Content, string Remaining) ExtractNamedSlot(string text, string tagName)
    {
        var open = Regex.Match(text, $@"<{Regex.Escape(tagName)}(?<attrs>\s[^>]*?)?\s*(?<self>/)?>");
        if (!open.Success) return (null, text);

        if (open.Groups["self"].Success)
        {
            // Self-closing → empty content, remove the tag.
            var head = text.Substring(0, open.Index);
            var tail = text.Substring(open.Index + open.Length);
            return ("", head + tail);
        }

        // Find matching close, tracking nested opens of the same name.
        var closeName = Regex.Escape(tagName);
        var scanFrom = open.Index + open.Length;
        var depth = 1;
        var openRe = new Regex($@"<{closeName}(\s[^>]*?)?\s*(?<self>/)?>");
        var closeRe = new Regex($@"</{closeName}\s*>");
        while (depth > 0)
        {
            var nextOpen = openRe.Match(text, scanFrom);
            var nextClose = closeRe.Match(text, scanFrom);
            if (!nextClose.Success) return (null, text);
            if (nextOpen.Success && nextOpen.Index < nextClose.Index)
            {
                if (!nextOpen.Groups["self"].Success) depth++;
                scanFrom = nextOpen.Index + nextOpen.Length;
            }
            else
            {
                depth--;
                if (depth == 0)
                {
                    var innerStart = open.Index + open.Length;
                    var inner = text.Substring(innerStart, nextClose.Index - innerStart);
                    var head = text.Substring(0, open.Index);
                    var tail = text.Substring(nextClose.Index + nextClose.Length);
                    return (inner, head + tail);
                }
                scanFrom = nextClose.Index + nextClose.Length;
            }
        }
        return (null, text);
    }

    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _propCache = new();

    internal static Dictionary<string, PropertyInfo> GetParameterProps(Type t)
    {
        lock (_propCache)
        {
            if (_propCache.TryGetValue(t, out var cached)) return cached;
            var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetCustomAttribute<ParameterAttribute>() is not null) map[p.Name] = p;
            }
            _propCache[t] = map;
            return map;
        }
    }

    internal static object Coerce(string raw, Type target)
    {
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        if (underlying == typeof(string)) return raw;
        if (underlying == typeof(bool)) return bool.Parse(raw);
        if (underlying.IsEnum) return Enum.Parse(underlying, raw, ignoreCase: true);
        if (underlying == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
        if (underlying == typeof(long)) return long.Parse(raw, CultureInfo.InvariantCulture);
        if (underlying == typeof(double)) return double.Parse(raw, CultureInfo.InvariantCulture);
        if (underlying == typeof(decimal)) return decimal.Parse(raw, CultureInfo.InvariantCulture);
        return raw;
    }
}
