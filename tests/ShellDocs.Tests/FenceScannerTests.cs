using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

// The slot extractor has to agree with Markdig about where a fenced block starts
// and ends, or everything after a misread fence is masked or scanned wrongly.
public class FenceScannerTests
{
    public class Callout { }
    public class Button { }

    private static MarkdownRenderer Renderer() => new(new TypeRegistry().Register<Callout>().Register<Button>());

    [Fact]
    public void LongerFence_ShowingAPreviewFence_LeavesTheNextPreviewLive()
    {
        var doc = Renderer().Render(
            "````markdown\n```razor:preview\n<Callout>example</Callout>\n```\n````\n\n" +
            "```razor:preview\n<Callout>live</Callout>\n```\n");

        var preview = Assert.IsType<PreviewSlot>(Assert.Single(doc.Slots));
        Assert.Contains("live", preview.Code);
        Assert.Contains("&lt;Callout&gt;example", doc.Html);
        Assert.DoesNotContain("SHELLDOCS_MASK", doc.Html);
    }

    [Fact]
    public void LongerFence_ShowingTagsAroundFences_KeepsTheTagsLiteral()
    {
        var doc = Renderer().Render(
            "````markdown\n<Callout>\n\n```bash\nls\n```\n\n</Callout>\n<Button />\n````\n\nThen <Button />.\n");

        Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
        Assert.Contains("&lt;Button /&gt;", doc.Html);
        Assert.Contains("&lt;/Callout&gt;", doc.Html);
    }

    [Fact]
    public void TildeFence_ShowingAPreviewFence_IsOneCodeBlock()
    {
        var doc = Renderer().Render("~~~markdown\n```razor:preview\n<Callout>example</Callout>\n```\n~~~\n\n<Button />\n");

        Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
        Assert.Contains("&lt;Callout&gt;example", doc.Html);
    }

    [Fact]
    public void TildeFence_CanBeAPreview()
    {
        var doc = Renderer().Render("~~~razor:preview\n<Callout>live</Callout>\n~~~\n");

        Assert.IsType<PreviewSlot>(Assert.Single(doc.Slots));
    }

    [Fact]
    public void ClosingFence_MayBeLongerThanTheOpeningOne()
    {
        var doc = Renderer().Render("```js\nconst a = '<Button />';\n`````\n\n<Button />\n");

        Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
    }

    [Fact]
    public void EmptyFence_DoesNotPairWithALaterFence()
    {
        var doc = Renderer().Render("```\n```\n\n<Button />\n\n```js\nconst a = 1;\n```\n");

        Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
    }

    [Fact]
    public void ThreeBacktickFence_StillWorks()
    {
        var doc = Renderer().Render("```razor\n<Button />\n```\n\n```razor:preview stretch\n<Callout>live</Callout>\n```\n");

        var preview = Assert.IsType<PreviewSlot>(Assert.Single(doc.Slots));
        Assert.Equal("stretch", preview.Layout);
        Assert.Contains("&lt;Button /&gt;", doc.Html);
    }
}
