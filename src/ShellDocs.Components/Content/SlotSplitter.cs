using System.Text.RegularExpressions;
using ShellDocs.Markdown;

namespace ShellDocs.Components.Content;

public static class SlotSplitter
{
    private static readonly Regex PlaceholderRegex = new(
        @"<div\s+data-shelldocs-slot=""(?<kind>[^""]+)""\s+data-shelldocs-id=""(?<id>[^""]+)""\s*></div>",
        RegexOptions.Compiled);

    public static IReadOnlyList<RenderPart> Split(RenderedDocument document)
    {
        var byId = document.Slots.ToDictionary(s => s.Id);
        var parts = new List<RenderPart>();
        var cursor = 0;

        foreach (Match m in PlaceholderRegex.Matches(document.Html))
        {
            if (m.Index > cursor)
            {
                var html = document.Html.Substring(cursor, m.Index - cursor);
                if (html.Length > 0) parts.Add(new HtmlPart(html));
            }
            if (byId.TryGetValue(m.Groups["id"].Value, out var slot))
            {
                parts.Add(new SlotPart(slot));
            }
            cursor = m.Index + m.Length;
        }

        if (cursor < document.Html.Length)
        {
            parts.Add(new HtmlPart(document.Html[cursor..]));
        }

        return parts;
    }
}

public abstract record RenderPart;
public record HtmlPart(string Html) : RenderPart;
public record SlotPart(Slot Slot) : RenderPart;
