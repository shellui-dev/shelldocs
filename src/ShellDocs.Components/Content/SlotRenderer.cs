using System.Globalization;
using System.Reflection;
using System.Text;
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
            dict["ChildContent"] = FromMarkup(renderer, childContentRaw);
        }
        return dict;
    }

    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _propCache = new();

    private static Dictionary<string, PropertyInfo> GetParameterProps(Type t)
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

    private static object Coerce(string raw, Type target)
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
