using System.Net;
using System.Text;
using ShellDocs.Markdown;

namespace ShellDocs.Components.Content;

// One tab of a preview's Code panel, e.g. the MAUI or Avalonia version of an example.
public sealed record PreviewCodeTab(string Label, string Code, string Language = "text");

/* Reads <CodeTab Label="…" [Language="…"]> blocks out of a preview's raw child
   markup. A tab's code is its first fenced block (language from the fence info),
   or the tag's text when there's no fence. */
internal static class CodeTabParser
{
    private const string TagName = "CodeTab";

    public static (IReadOnlyList<PreviewCodeTab> Tabs, string Remaining) Extract(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || !raw.Contains('<' + TagName, StringComparison.Ordinal))
            return (Array.Empty<PreviewCodeTab>(), raw ?? "");

        var tabs = new List<PreviewCodeTab>();
        var rest = new StringBuilder(raw.Length);
        var i = 0;
        while (i < raw.Length)
        {
            var lt = raw.IndexOf('<' + TagName, i, StringComparison.Ordinal);
            if (lt < 0) break;
            if (!RazorTagScanner.TryRead(raw, lt, out var tag) || tag.IsClose || tag.Name != TagName)
            {
                rest.Append(raw, i, lt + 1 - i);
                i = lt + 1;
                continue;
            }

            rest.Append(raw, i, lt - i);
            var body = "";
            var end = tag.End;
            if (!tag.IsSelfClosing)
            {
                var (closeStart, closeEnd) = RazorTagScanner.FindMatchingClose(raw, TagName, tag.End, StringComparison.Ordinal);
                body = closeStart < 0 ? raw[tag.End..] : raw[tag.End..closeStart];
                end = closeStart < 0 ? raw.Length : closeEnd;
            }

            var label = Attribute(tag, "Label");
            if (!string.IsNullOrWhiteSpace(label))
                tabs.Add(FromBody(label, Attribute(tag, "Language"), body));
            i = end;
        }
        rest.Append(raw, i, raw.Length - i);
        return (tabs, rest.ToString());
    }

    private static PreviewCodeTab FromBody(string label, string? language, string body)
    {
        var fence = SlotExtractor.FenceBlock.Match(body);
        string code;
        if (fence.Success)
        {
            code = StripIndent(fence.Groups["body"].Value, fence.Groups["indent"].Length);
            var info = fence.Groups["lang"].Value.Trim();
            language ??= info.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        else
        {
            code = Dedent(body);
        }
        return new PreviewCodeTab(label.Trim(), code.Trim('\r', '\n'), NormalizeLanguage(language));
    }

    private static string? Attribute(RazorTag tag, string name)
    {
        foreach (var (key, value) in tag.Attributes)
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                return value is null ? null : WebUtility.HtmlDecode(value);
        return null;
    }

    private static string NormalizeLanguage(string? language)
        => string.IsNullOrWhiteSpace(language) ? "text" : language.Trim().ToLowerInvariant();

    private static string StripIndent(string text, int indent)
    {
        if (indent == 0) return text;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var n = 0; n < lines.Length; n++)
        {
            var strip = 0;
            while (strip < indent && strip < lines[n].Length && lines[n][strip] is ' ' or '\t') strip++;
            lines[n] = lines[n][strip..];
        }
        return string.Join('\n', lines);
    }

    private static string Dedent(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var indent = lines.Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Length - l.TrimStart(' ', '\t').Length)
            .DefaultIfEmpty(0)
            .Min();
        return StripIndent(string.Join('\n', lines), indent);
    }
}
