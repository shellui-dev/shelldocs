using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ShellDocs.Components.Content;
using ShellDocs.Core;
using ShellDocs.Markdown;

namespace ShellDocs.Components.Llms;

/* A page's Markdown body as plain Markdown for AI agents. ShellDocs tags become
   Markdown (a callout a blockquote, a demo its source in a fence, cards a list),
   other registered components their text, and root-relative links absolute URLs,
   with ".md" added for docs pages. Code fences and code spans are left alone. */
internal sealed partial class LlmsMarkdown
{
    // Every built-in content component is handled here; a test checks the list stays complete.
    internal static readonly IReadOnlySet<Type> HandledTypes = new HashSet<Type>
    {
        typeof(AutoTypeTable), typeof(Callout), typeof(Card), typeof(CardGrid), typeof(CodeGroup),
        typeof(CodeTab), typeof(ComponentPreview), typeof(DemoPreview), typeof(FileTree),
        typeof(FileTreeItem), typeof(IframePreview), typeof(LinkCard), typeof(Step), typeof(Steps),
        typeof(SyncSwitch), typeof(Tab), typeof(Tabs), typeof(TypeRow), typeof(TypeTable),
    };

    // Preview attributes that configure the frame rather than the component shown.
    private static readonly HashSet<string> FrameAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Component", "Layout", "SyncKey", "Caption", "Title", "Id", "CodeTabs", "ChildContentSource",
    };

    private readonly TypeRegistry _registry;
    private readonly NavigationGraph _graph;
    private readonly string? _demoRoot;
    private readonly string _base;
    private readonly Dictionary<string, string> _masks = new();
    private readonly Stack<int> _steps = new();

    // `baseUrl` is put before root-relative links: an origin, an origin and path, or "".
    public LlmsMarkdown(TypeRegistry registry, NavigationGraph graph, string? demoRoot, string baseUrl)
    {
        _registry = registry;
        _graph = graph;
        _demoRoot = demoRoot;
        _base = baseUrl.TrimEnd('/');
    }

    public string Convert(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n");
        text = SlotExtractor.FenceBlock.Replace(text, m => Mask(AsPlainFence(m)));
        text = SlotExtractor.InlineCode.Replace(text, m => Mask(m.Value));
        text = Tags(text);
        text = Links(text);
        text = Unmask(text);
        return BlankRun().Replace(text, "\n\n").Trim();
    }

    // Absolute URL for an app-relative path; docs pages get ".md".
    public string Url(string path, bool markdown = true)
    {
        var p = UrlPath.Normalize(path);
        var isPage = markdown && _graph.ResolveByUrl(p) is { Kind: NodeKind.Page };
        return _base + (p == "/" && !isPage ? "/" : p) + (isPage ? ".md" : "");
    }

    // ```razor:preview becomes ```razor: the fence's body is the example's source.
    private static string AsPlainFence(Match m)
    {
        var lang = m.Groups["lang"];
        if (!lang.Value.TrimStart().StartsWith("razor:preview", StringComparison.Ordinal)) return m.Value;
        var start = lang.Index - m.Index;
        return m.Value[..start] + "razor" + m.Value[(start + lang.Length)..];
    }

    private string Tags(string text)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var lt = text.IndexOf('<', i);
            if (lt < 0) { sb.Append(text, i, text.Length - i); break; }
            sb.Append(text, i, lt - i);

            if (!RazorTagScanner.TryRead(text, lt, out var tag) || tag.IsClose || _registry.Resolve(tag.Name) is not { } type)
            {
                sb.Append('<');
                i = lt + 1;
                continue;
            }

            var inner = "";
            var end = tag.End;
            if (!tag.IsSelfClosing)
            {
                var (closeStart, closeEnd) = RazorTagScanner.FindMatchingClose(text, tag.Name, tag.End, StringComparison.Ordinal);
                if (closeStart < 0) { sb.Append('<'); i = lt + 1; continue; }
                inner = text[tag.End..closeStart];
                end = closeEnd;
            }
            sb.Append(Render(type, tag, inner));
            i = end;
        }
        return sb.ToString();
    }

    private string Render(Type type, RazorTag tag, string inner)
    {
        string? A(string name) => Attr(tag, name);

        if (type == typeof(Callout))
        {
            var title = A("Title") ?? (A("Variant")?.ToLowerInvariant() switch
            {
                "warning" => "Warning",
                "danger" or "error" => "Danger",
                "tip" => "Tip",
                "success" => "Success",
                _ => "Note",
            });
            var body = Unmask(A("Text") is { } t && inner.Trim().Length == 0 ? t : Tags(inner)).Trim();
            var quoted = body.Split('\n').Select(l => l.Trim().Length == 0 ? ">" : "> " + l);
            return Block($"> **{title}**" + (body.Length == 0 ? "" : "\n>\n" + string.Join("\n", quoted)));
        }

        if (type == typeof(Tab) || type == typeof(CodeTab))
            return Block($"**{A("Label")}**\n\n{Tags(inner).Trim()}");

        if (type == typeof(Tabs) || type == typeof(CodeGroup))
            return Block(Tags(inner));

        if (type == typeof(CardGrid))
            return Block(string.Join("\n", Tags(inner).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)));

        if (type == typeof(Card) || type == typeof(LinkCard))
        {
            var title = A("Title") ?? "";
            var line = "- " + (A("Href") is { Length: > 0 } href ? $"[{title}]({href})" : $"**{title}**");
            var text = A("Description") ?? OneLine(Tags(inner));
            if (text.Length > 0) line += ": " + text;
            return "\n" + line + "\n";
        }

        if (type == typeof(Steps))
        {
            _steps.Push(0);
            var steps = Tags(inner);
            _steps.Pop();
            return Block(steps);
        }

        if (type == typeof(Step))
        {
            var n = 0;
            if (_steps.Count > 0) _steps.Push(n = _steps.Pop() + 1);
            var heading = n > 0 ? $"{n}. {A("Title")}" : A("Title");
            return Block($"**{heading}**\n\n{Tags(inner).Trim()}");
        }

        if (type == typeof(FileTree))
            return Block(Mask("```text\n" + string.Join("\n", TreeLines(inner, 0)) + "\n```"));

        if (type == typeof(FileTreeItem))
            return FileTreeName(tag);

        if (type == typeof(TypeTable))
        {
            var rows = Tags(inner).Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith('|'));
            return Block(TableHeader + string.Join("\n", rows));
        }

        if (type == typeof(TypeRow))
            return "\n" + Row(A("Name") ?? "", A("Type") ?? "", A("Default"), A("Description"), IsTrue(A("Required"))) + "\n";

        if (type == typeof(AutoTypeTable))
        {
            var target = A("Component") is { } c ? _registry.Resolve(c) : null;
            if (target is null) return "";
            var rows = AutoTypeTable.BuildRows(target).Select(r => Row(r.Name, r.Type, r.Default, r.Description, r.Required));
            return Block(TableHeader + string.Join("\n", rows));
        }

        if (type == typeof(DemoPreview) || type == typeof(ComponentPreview) || type == typeof(IframePreview))
        {
            var title = A("Title") is { Length: > 0 } t ? $"**{t}**\n\n" : "";
            if (HasChild(inner, typeof(CodeTab))) return Block(title + Tags(inner));
            if (type == typeof(DemoPreview))
            {
                var source = A("Component") is { } name && _demoRoot is not null ? DemoSourceCache.Get(_demoRoot, name) : null;
                return source is null ? Block(title) : Block(title + Fence("razor", source));
            }
            if (type == typeof(IframePreview))
                return Block(title + $"[Live preview]({A("Src")})");
            return Block(title + Fence("razor", PreviewMarkup(tag, Unmask(inner).Trim())));
        }

        if (type == typeof(SyncSwitch))
            return "";

        // Any other registered component: its text, or nothing when it has no body.
        return Tags(inner);
    }

    // <ComponentPreview Component="Button" Variant="Outline">Save</ComponentPreview> → <Button Variant="Outline">Save</Button>
    private string PreviewMarkup(RazorTag tag, string body)
    {
        var name = Attr(tag, "Component") ?? "";
        var attrs = string.Concat(tag.Attributes
            .Where(a => !FrameAttributes.Contains(a.Key))
            .Select(a => a.Value is null ? " " + a.Key : $" {a.Key}=\"{Unmask(a.Value)}\""));
        return body.Length == 0 ? $"<{name}{attrs} />" : $"<{name}{attrs}>{body}</{name}>";
    }

    private IEnumerable<string> TreeLines(string inner, int depth)
    {
        foreach (var (tag, body) in Children(inner, typeof(FileTreeItem)))
        {
            yield return new string(' ', depth * 2) + FileTreeName(tag);
            foreach (var line in TreeLines(body, depth + 1)) yield return line;
        }
    }

    private string FileTreeName(RazorTag tag)
    {
        var name = (Attr(tag, "Name") ?? "") + (IsTrue(Attr(tag, "IsFolder")) ? "/" : "");
        return Attr(tag, "Comment") is { Length: > 0 } comment ? $"{name}  # {comment}" : name;
    }

    private bool HasChild(string inner, Type type) => Children(inner, type).Any();

    // Top-level tags in `inner` that resolve to `type`, with their bodies.
    private IEnumerable<(RazorTag Tag, string Body)> Children(string inner, Type type)
    {
        var i = 0;
        while (i < inner.Length)
        {
            var lt = inner.IndexOf('<', i);
            if (lt < 0) yield break;
            if (!RazorTagScanner.TryRead(inner, lt, out var tag) || tag.IsClose) { i = lt + 1; continue; }
            var body = "";
            var end = tag.End;
            if (!tag.IsSelfClosing)
            {
                var (closeStart, closeEnd) = RazorTagScanner.FindMatchingClose(inner, tag.Name, tag.End, StringComparison.Ordinal);
                if (closeStart >= 0) { body = inner[tag.End..closeStart]; end = closeEnd; }
            }
            if (_registry.Resolve(tag.Name) == type) yield return (tag, body);
            i = end;
        }
    }

    private const string TableHeader = "| Name | Type | Default | Description |\n|------|------|---------|-------------|\n";

    private static string Row(string name, string type, string? def, string? description, bool required) =>
        $"| `{Cell(name)}`{(required ? " (required)" : "")} | `{Cell(type)}` | {(string.IsNullOrEmpty(def) ? "" : $"`{Cell(def)}`")} | {Cell(description ?? "")} |";

    private static string Cell(string s) => s.Replace("|", "\\|").Replace("\n", " ");

    private string Fence(string lang, string code)
    {
        var body = code.Replace("\r\n", "\n").Trim('\n');
        var ticks = new string('`', Math.Max(3, LongestBacktickRun(body) + 1));
        return Mask($"{ticks}{lang}\n{body}\n{ticks}");
    }

    private static int LongestBacktickRun(string s)
    {
        int best = 0, run = 0;
        foreach (var c in s) { run = c == '`' ? run + 1 : 0; best = Math.Max(best, run); }
        return best;
    }

    private string? Attr(RazorTag tag, string name)
    {
        foreach (var (key, value) in tag.Attributes)
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                return value is null ? "" : WebUtility.HtmlDecode(Unmask(value));
        return null;
    }

    private static bool IsTrue(string? value) => value is "" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private string OneLine(string text) => Whitespace().Replace(Unmask(text), " ").Trim();

    private static string Block(string text) => "\n\n" + text.Trim('\n') + "\n\n";

    private string Links(string text) => Link().Replace(text, m =>
        m.Groups["pre"].Value + Url(m.Groups["path"].Value) + m.Groups["rest"].Value);

    private string Mask(string original)
    {
        var id = $"\uE000{_masks.Count}\uE001";
        _masks[id] = original;
        return id;
    }

    // Masks can hold masks (a fence moved into a callout), so this repeats until none are left.
    private string Unmask(string text)
    {
        while (text.Contains('\uE000'))
        {
            var next = MaskToken().Replace(text, m => _masks.TryGetValue(m.Value, out var v) ? v : m.Value);
            if (next == text) break;
            text = next;
        }
        return text;
    }

    [GeneratedRegex("\uE000\\d+\uE001")]
    private static partial Regex MaskToken();

    // Markdown link targets and href / src attributes that start with a single "/".
    [GeneratedRegex(@"(?<pre>\]\(\s*|\b(?:href|Href|src|Src)\s*=\s*"")(?<path>/(?!/)[^)\s""#?]*)(?<rest>[?#][^)\s""]*)?")]
    private static partial Regex Link();

    [GeneratedRegex(@"\n[ \t]*(?:\n[ \t]*)+\n")]
    private static partial Regex BlankRun();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
