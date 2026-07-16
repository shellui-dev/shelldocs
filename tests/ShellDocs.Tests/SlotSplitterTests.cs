using ShellDocs.Components.Content;
using ShellDocs.Core;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class SlotSplitterTests
{
    public class Fake : Microsoft.AspNetCore.Components.ComponentBase { }

    private static RenderedDocument DocumentWith(string html, params Slot[] slots) =>
        new(html, slots, new ParsedDocument(new Dictionary<string, object?>(), ""), Array.Empty<Heading>());

    [Fact]
    public void Split_HtmlOnly_ReturnsSingleHtmlPart()
    {
        var doc = DocumentWith("<p>Hello world</p>");
        var parts = SlotSplitter.Split(doc);

        var only = Assert.Single(parts);
        var html = Assert.IsType<HtmlPart>(only);
        Assert.Equal("<p>Hello world</p>", html.Html);
    }

    [Fact]
    public void Split_SlotOnly_ReturnsSingleSlotPart()
    {
        var slot = new ComponentSlot("sabc", typeof(Fake), new Dictionary<string, string>(), null);
        var doc = DocumentWith(
            "<div data-shelldocs-slot=\"component\" data-shelldocs-id=\"sabc\"></div>",
            slot);

        var parts = SlotSplitter.Split(doc);

        var only = Assert.Single(parts);
        var sp = Assert.IsType<SlotPart>(only);
        Assert.Same(slot, sp.Slot);
    }

    [Fact]
    public void Split_MixedContent_InterleavesInOrder()
    {
        var slot1 = new ComponentSlot("s1", typeof(Fake), new Dictionary<string, string>(), null);
        var slot2 = new PreviewSlot("s2", typeof(Fake), new Dictionary<string, string>(), "code", "razor");
        var html =
            "<p>Before</p>" +
            "<div data-shelldocs-slot=\"component\" data-shelldocs-id=\"s1\"></div>" +
            "<p>Middle</p>" +
            "<div data-shelldocs-slot=\"preview\" data-shelldocs-id=\"s2\"></div>" +
            "<p>After</p>";
        var doc = DocumentWith(html, slot1, slot2);

        var parts = SlotSplitter.Split(doc);

        Assert.Equal(5, parts.Count);
        Assert.Equal("<p>Before</p>", ((HtmlPart)parts[0]).Html);
        Assert.Same(slot1, ((SlotPart)parts[1]).Slot);
        Assert.Equal("<p>Middle</p>", ((HtmlPart)parts[2]).Html);
        Assert.Same(slot2, ((SlotPart)parts[3]).Slot);
        Assert.Equal("<p>After</p>", ((HtmlPart)parts[4]).Html);
    }

    [Fact]
    public void Split_UnknownSlotId_DropsPlaceholder()
    {
        var html =
            "<p>A</p>" +
            "<div data-shelldocs-slot=\"component\" data-shelldocs-id=\"missing\"></div>" +
            "<p>B</p>";
        var doc = DocumentWith(html);

        var parts = SlotSplitter.Split(doc);

        Assert.Equal(2, parts.Count);
        Assert.Equal("<p>A</p>", ((HtmlPart)parts[0]).Html);
        Assert.Equal("<p>B</p>", ((HtmlPart)parts[1]).Html);
    }

    [Fact]
    public void Split_EmptyHtml_ReturnsEmpty()
    {
        var doc = DocumentWith("");
        Assert.Empty(SlotSplitter.Split(doc));
    }
}
