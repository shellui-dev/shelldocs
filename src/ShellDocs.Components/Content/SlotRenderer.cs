using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using ShellDocs.Markdown;

namespace ShellDocs.Components.Content;

// Sequence numbers follow the parsed markup, not source order; regions per node keep them stable.
#pragma warning disable ASP0006

// Renders markdown/HTML containing component tags as real DynamicComponents,
// recursively, so components nest inside previews and ChildContent.
internal static class SlotRenderer
{
    public static RenderFragment FromMarkup(MarkdownRenderer renderer, string raw, ILogger? logger = null) => builder =>
    {
        /* Markdig treats 4+ space indentation as a code block, so children
           authored at the outer tag's indent would render as <pre>. */
        var doc = renderer.Render(Dedent(raw));
        // As in MDX, a body on the tags' own line is inline: <Badge>v1</Badge> gets no <p>.
        if (!raw.Contains('\n')) doc = doc with { Html = Unwrap(doc.Html) };
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
                Emit(builder, ref seq, renderer, comp, logger);
            }
        }
    };

    // The inside of a single <p>…</p>; anything else is returned as is.
    internal static string Unwrap(string html)
    {
        var t = html.Trim();
        if (!t.StartsWith("<p>", StringComparison.Ordinal) || !t.EndsWith("</p>", StringComparison.Ordinal)) return html;
        var inner = t[3..^4];
        return inner.Contains("<p>", StringComparison.Ordinal) || inner.Contains("</p>", StringComparison.Ordinal) ? html : inner;
    }

    // Child content inside a razor:preview is Razor, not markdown.
    public static RenderFragment FromRazor(MarkdownRenderer renderer, string raw, ILogger? logger = null)
    {
        var (nodes, warnings) = renderer.ParseRazor(raw);
        foreach (var warning in warnings) logger?.LogWarning("ShellDocs: {Warning}", warning);
        return RenderNodes(renderer, nodes, logger);
    }

    private static void Emit(RenderTreeBuilder builder, ref int seq, MarkdownRenderer renderer, ComponentSlot slot, ILogger? logger)
        => builder.AddContent(seq++, Component(renderer, slot.ComponentType, slot.Parameters, slot.ChildContentRaw, logger));

    /* One registered component from markdown. Generic definitions are closed from
       their type-parameter attributes first; one that can't be closed renders a
       visible error instead of throwing. */
    public static RenderFragment Component(
        MarkdownRenderer renderer,
        Type type,
        IReadOnlyDictionary<string, string> attrs,
        string? childContentRaw,
        ILogger? logger = null,
        bool razorChildren = false) => builder =>
    {
        var closed = GenericComponents.Close(type, attrs, out var remaining, out var error, out var notes);
        foreach (var note in notes) logger?.LogWarning("ShellDocs: {Note}", note);
        if (closed is null)
        {
            logger?.LogWarning("ShellDocs: {Error}", error);
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "shelldocs-render-error");
            builder.AddAttribute(2, "role", "alert");
            builder.AddContent(3, error);
            builder.CloseElement();
            return;
        }
        builder.OpenComponent<DynamicComponent>(4);
        builder.AddAttribute(5, "Type", closed);
        builder.AddAttribute(6, "Parameters", BuildParameters(renderer, closed, remaining, childContentRaw, logger, razorChildren));
        builder.CloseComponent();
    };

    /* Elements are real render-tree elements, not markup strings, so wrappers keep
       their component children under interactive re-renders. One region per node
       keeps sequence numbers stable. */
    public static RenderFragment RenderNodes(MarkdownRenderer renderer, IReadOnlyList<PreviewNode> nodes, ILogger? logger = null)
        => builder => EmitNodes(builder, renderer, nodes, logger);

    private static void EmitNodes(RenderTreeBuilder builder, MarkdownRenderer renderer, IReadOnlyList<PreviewNode> nodes, ILogger? logger)
    {
        for (var n = 0; n < nodes.Count; n++)
        {
            builder.OpenRegion(n);
            switch (nodes[n])
            {
                case PreviewTextNode text:
                    builder.AddContent(0, text.Text);
                    break;

                case PreviewComponentNode comp:
                    builder.AddContent(0, Component(renderer, comp.ComponentType, comp.Parameters, comp.ChildContentRaw, logger, razorChildren: true));
                    break;

                case PreviewElementNode el:
                    var seq = 0;
                    builder.OpenElement(seq++, el.TagName);
                    foreach (var (name, value) in el.Attributes)
                    {
                        if (value is null) builder.AddAttribute(seq++, name, true);
                        else builder.AddAttribute(seq++, name, value);
                    }
                    if (el.Children.Count > 0)
                    {
                        builder.OpenRegion(seq++);
                        EmitNodes(builder, renderer, el.Children, logger);
                        builder.CloseRegion();
                    }
                    builder.CloseElement();
                    break;
            }
            builder.CloseRegion();
        }
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

    /* Never throws. Anything static markup can't set — directive attributes,
       EventCallback/delegate params, unsupported types, unparseable values, unknown
       attributes without a CaptureUnmatchedValues catch-all — is skipped with a
       warning and the component still renders. With razorChildren (razor:preview)
       child content is parsed as Razor; otherwise it's markdown, as in prose. */
    public static IDictionary<string, object> BuildParameters(
        MarkdownRenderer renderer,
        Type componentType,
        IReadOnlyDictionary<string, string> attrs,
        string? childContentRaw,
        ILogger? logger = null,
        bool razorChildren = false)
    {
        RenderFragment Fragment(string raw) => razorChildren
            ? FromRazor(renderer, raw, logger)
            : FromMarkup(renderer, raw, logger);

        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        var props = GetParameterProps(componentType);
        var catchAll = HasCatchAll(componentType);
        foreach (var (k, v) in attrs)
        {
            if (k.StartsWith('@'))
            {
                Skip(logger, componentType, k, "Razor directive attributes can't be evaluated in a static preview");
                continue;
            }
            if (!props.TryGetValue(k, out var prop))
            {
                if (catchAll) dict[k] = v;
                else Skip(logger, componentType, k, "no matching [Parameter] and no CaptureUnmatchedValues catch-all");
                continue;
            }
            if (!IsCoercible(prop.PropertyType))
            {
                Skip(logger, componentType, k, $"parameter type {prop.PropertyType.Name} can't be set from a string attribute");
                continue;
            }
            if (!TryCoerce(v, prop.PropertyType, out var coerced))
            {
                Skip(logger, componentType, k, $"value \"{v}\" isn't a valid {prop.PropertyType.Name}");
                continue;
            }
            if (coerced is not null) dict[prop.Name] = coerced;
        }
        if (!string.IsNullOrWhiteSpace(childContentRaw))
        {
            // Child tags named after a RenderFragment parameter (<Icon> → Alert.Icon)
            // go to that slot; the rest becomes ChildContent.
            var slotNames = props
                .Where(kv => kv.Key != "ChildContent" && kv.Value.PropertyType == typeof(RenderFragment))
                .Select(kv => kv.Value.Name)
                .ToHashSet(StringComparer.Ordinal);

            var remaining = childContentRaw;
            if (slotNames.Count > 0)
            {
                foreach (var slotName in slotNames)
                {
                    var extracted = ExtractNamedSlot(remaining, slotName);
                    if (extracted.Content is not null)
                    {
                        dict[slotName] = Fragment(extracted.Content);
                        remaining = extracted.Remaining;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(remaining))
            {
                // Only a plain RenderFragment ChildContent can take markup; a
                // missing or templated (RenderFragment<T>) one would throw.
                if (props.TryGetValue("ChildContent", out var cc) && cc.PropertyType == typeof(RenderFragment))
                    dict["ChildContent"] = Fragment(remaining);
                else
                    Skip(logger, componentType, "ChildContent", "component has no RenderFragment ChildContent parameter");
            }

            // ComponentPreview rebuilds its source view from the raw markup.
            if (props.ContainsKey("ChildContentSource"))
                dict["ChildContentSource"] = childContentRaw;
        }
        return dict;
    }

    // Removes the first balanced `<TagName>…</TagName>` (or `<TagName />`) from
    // `text`, returning its inner content and the remaining text.
    // Tags are read with RazorTagScanner, so a '>' inside a quoted attribute value doesn't end one.
    internal static (string? Content, string Remaining) ExtractNamedSlot(string text, string tagName)
    {
        for (var lt = text.IndexOf('<'); lt >= 0; lt = text.IndexOf('<', lt + 1))
        {
            if (!RazorTagScanner.TryRead(text, lt, out var open) || open.IsClose) continue;
            if (!string.Equals(open.Name, tagName, StringComparison.Ordinal)) continue;

            if (open.IsSelfClosing) return ("", text[..open.Start] + text[open.End..]);

            var (closeStart, closeEnd) = RazorTagScanner.FindMatchingClose(text, tagName, open.End, StringComparison.Ordinal);
            if (closeStart < 0) return (null, text);
            return (text[open.End..closeStart], text[..open.Start] + text[closeEnd..]);
        }
        return (null, text);
    }

    private static void Skip(ILogger? logger, Type componentType, string attribute, string reason)
        => logger?.LogWarning("ShellDocs: skipped attribute '{Attribute}' on <{Component}> — {Reason}.",
            attribute, componentType.Name, reason);

    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> _propCache = new();
    private static readonly Dictionary<Type, bool> _catchAllCache = new();

    // Blazor matches parameter names case-insensitively, so lookups here do too.
    internal static Dictionary<string, PropertyInfo> GetParameterProps(Type t)
    {
        lock (_propCache)
        {
            if (_propCache.TryGetValue(t, out var cached)) return cached;
            var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetCustomAttribute<ParameterAttribute>() is not null) map[p.Name] = p;
            }
            _propCache[t] = map;
            return map;
        }
    }

    internal static bool HasCatchAll(Type t)
    {
        lock (_catchAllCache)
        {
            if (_catchAllCache.TryGetValue(t, out var cached)) return cached;
            var has = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.GetCustomAttribute<ParameterAttribute>()?.CaptureUnmatchedValues == true);
            _catchAllCache[t] = has;
            return has;
        }
    }

    internal static bool IsCoercible(Type target)
    {
        var t = Nullable.GetUnderlyingType(target) ?? target;
        if (t.IsAssignableFrom(typeof(string))) return true;
        if (t.IsEnum) return true;
        return Type.GetTypeCode(t) is TypeCode.Boolean or TypeCode.Char
            or TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
            or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
    }

    /* Accepts what Razor authors write: a leading '@' or @( … ), Type.Member enum
       values, [Flags] combined with '|', numeric suffixes (1.5f, 10m) and @null.
       Throws on bad values; BuildParameters goes through TryCoerce instead. */
    internal static object? Coerce(string raw, Type target)
    {
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        if (underlying == typeof(string)) return raw;

        var v = StripRazor(raw);
        var nullable = !target.IsValueType || Nullable.GetUnderlyingType(target) is not null;
        if (v == "null" && nullable) return null;
        if (underlying.IsAssignableFrom(typeof(string))) return raw;
        if (underlying == typeof(bool)) return v.Length == 0 || bool.Parse(v); // bare attribute → true
        if (underlying.IsEnum) return ParseEnum(underlying, v);
        if (underlying == typeof(char))
        {
            var ch = v.Trim('\'');
            if (ch.Length != 1) throw new FormatException($"'{raw}' is not a single character.");
            return ch[0];
        }
        if (IsCoercible(underlying))
        {
            var num = v.Replace("_", "");
            try { return Convert.ChangeType(num, underlying, CultureInfo.InvariantCulture); }
            catch (FormatException)
            {
                var trimmed = num.TrimEnd('f', 'F', 'd', 'D', 'm', 'M', 'l', 'L', 'u', 'U');
                return Convert.ChangeType(trimmed, underlying, CultureInfo.InvariantCulture);
            }
        }
        throw new NotSupportedException($"Can't coerce an attribute string to {target.Name}.");
    }

    internal static bool TryCoerce(string raw, Type target, out object? value)
    {
        try
        {
            value = Coerce(raw, target);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException
                                       or InvalidCastException or NotSupportedException)
        {
            value = null;
            return false;
        }
    }

    private static string StripRazor(string raw)
    {
        var v = raw.Trim();
        if (v.StartsWith('@')) v = v[1..].Trim();
        if (v.StartsWith('(') && v.EndsWith(')')) v = v[1..^1].Trim();
        return v;
    }

    private static object ParseEnum(Type enumType, string v)
    {
        var parts = v.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) throw new FormatException($"Empty value for enum {enumType.Name}.");
        if (parts.Length == 1) return Enum.Parse(enumType, Member(parts[0]), ignoreCase: true);

        var unsigned = Type.GetTypeCode(Enum.GetUnderlyingType(enumType)) is TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64;
        ulong bits = 0;
        foreach (var part in parts)
        {
            var parsed = Enum.Parse(enumType, Member(part), ignoreCase: true);
            bits |= unsigned
                ? Convert.ToUInt64(parsed, CultureInfo.InvariantCulture)
                : unchecked((ulong)Convert.ToInt64(parsed, CultureInfo.InvariantCulture));
        }
        return unsigned ? Enum.ToObject(enumType, bits) : Enum.ToObject(enumType, unchecked((long)bits));

        static string Member(string s) => s[(s.LastIndexOf('.') + 1)..];
    }
}
