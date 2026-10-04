using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

// Inline component tags in prose: quoted attribute values may hold anything,
// as they can in a razor:preview fence.
public class InlineTagAttributeTests
{
    public class Callout { }
    public class Button { }

    private static MarkdownRenderer Renderer() => new(new TypeRegistry().Register<Callout>().Register<Button>());

    [Fact]
    public void AngleBracketsInAQuotedValue_DoNotEndTheTag()
    {
        var doc = Renderer().Render("<Callout Title=\"a > b\" Text=\"wraps a <Button> tag\" />\n");

        var slot = Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
        Assert.Equal(typeof(Callout), slot.ComponentType);
        Assert.Equal("a > b", slot.Parameters["Title"]);
        Assert.Equal("wraps a <Button> tag", slot.Parameters["Text"]);
    }

    [Fact]
    public void AngleBracketsInAnOpeningTagValue_KeepTheBody()
    {
        var doc = Renderer().Render("<Callout Title=\"x -> y\">Body</Callout>\n");

        var slot = Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
        Assert.Equal("x -> y", slot.Parameters["Title"]);
        Assert.Equal("Body", slot.ChildContentRaw);
    }

    [Fact]
    public void QuotedTextThatLooksLikeAnAttribute_StaysInsideItsValue()
    {
        var doc = Renderer().Render("<Callout Text='rewrites href=\"/\" in the page' />\n");

        var slot = Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
        Assert.Equal("rewrites href=\"/\" in the page", slot.Parameters["Text"]);
        Assert.Single(slot.Parameters);
    }

    [Fact]
    public void CodeSpanWithQuotesInAValue_IsRestoredWhole()
    {
        var doc = Renderer().Render("<Callout Text=\"set `Variant='x'` here\" />\n");

        var slot = Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
        Assert.Equal("set `Variant='x'` here", slot.Parameters["Text"]);
        Assert.Single(slot.Parameters);
    }

    [Fact]
    public void UnterminatedQuote_DoesNotSwallowLaterParagraphs()
    {
        var doc = Renderer().Render("<Callout Title=\"oops />\n\nA \"quoted\" word, then <Button />.\n");

        var slot = Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
        Assert.Equal(typeof(Button), slot.ComponentType);
    }
}
