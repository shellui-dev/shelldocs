using ShellDocs.Core;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class MarkdownRendererTests
{
    public class Callout { }
    public class Button { }

    private static MarkdownRenderer WithComponents()
    {
        var reg = new TypeRegistry()
            .Register<Callout>()
            .Register<Button>();
        return new MarkdownRenderer(reg);
    }

    [Fact]
    public void Render_StandardMarkdown_ProducesHtml()
    {
        var doc = new MarkdownRenderer().Render("# Title\n\nParagraph.");
        Assert.Contains("<h1", doc.Html);
        Assert.Contains("Title", doc.Html);
        Assert.Contains("<p>", doc.Html);
    }

    [Fact]
    public void Render_ExposesFrontmatter()
    {
        var md = "---\ntitle: Button\norder: 3\n---\n# Body";
        var doc = new MarkdownRenderer().Render(md);
        Assert.Equal("Button", doc.Source.Frontmatter.GetValue<string>("title"));
        Assert.Equal(3, doc.Source.Frontmatter.GetValue<int>("order"));
        Assert.Contains("<h1", doc.Html);
    }

    [Fact]
    public void Render_SelfClosingInlineTag_ProducesComponentSlot()
    {
        var doc = WithComponents().Render("Click <Button Variant=\"Default\" /> now.");

        var slot = Assert.Single(doc.Slots);
        var comp = Assert.IsType<ComponentSlot>(slot);
        Assert.Equal(typeof(Button), comp.ComponentType);
        Assert.Equal("Default", comp.Parameters["Variant"]);
        Assert.Null(comp.ChildContentRaw);
        Assert.Contains("data-shelldocs-slot=\"component\"", doc.Html);
    }

    [Fact]
    public void Render_BlockTagWithBody_CapturesChildContentRaw()
    {
        var md = "<Callout Type=\"Info\">Body text here.</Callout>";
        var doc = WithComponents().Render(md);

        var comp = Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
        Assert.Equal(typeof(Callout), comp.ComponentType);
        Assert.Equal("Info", comp.Parameters["Type"]);
        Assert.Equal("Body text here.", comp.ChildContentRaw);
    }

    [Fact]
    public void Render_UnknownTag_LeftAsRawAndWarned()
    {
        var renderer = WithComponents();
        var doc = renderer.Render("Text with <Unknown Type=\"x\" /> tag.");

        Assert.Empty(doc.Slots);
        Assert.Contains(renderer.LastWarnings, w => w.Contains("Unknown"));
    }

    [Fact]
    public void Render_RazorPreviewFence_ProducesPreviewSlot()
    {
        var md = "Text.\n\n```razor:preview\n<Button Variant=\"Default\">Click</Button>\n```\n\nMore.";
        var renderer = WithComponents();
        var doc = renderer.Render(md);

        var preview = Assert.IsType<PreviewSlot>(Assert.Single(doc.Slots));
        Assert.Equal(typeof(Button), preview.ComponentType);
        Assert.Equal("Default", preview.Parameters["Variant"]);
        Assert.Contains("<Button", preview.Code);
        Assert.Equal("razor", preview.Language);
        Assert.Contains("data-shelldocs-slot=\"preview\"", doc.Html);
    }

    [Fact]
    public void Render_MultipleSlots_KeepsOrderStable()
    {
        var md = """
        # Docs

        <Button Variant="A" />

        Some paragraph.

        ```razor:preview
        <Callout Type="Info">Preview body</Callout>
        ```

        <Button Variant="B" />
        """;

        var doc = WithComponents().Render(md);
        Assert.Equal(3, doc.Slots.Count);
        var first = Assert.IsType<ComponentSlot>(doc.Slots[0]);
        var second = Assert.IsType<PreviewSlot>(doc.Slots[1]);
        var third = Assert.IsType<ComponentSlot>(doc.Slots[2]);
        Assert.Equal("A", first.Parameters["Variant"]);
        Assert.Equal(typeof(Callout), second.ComponentType);
        Assert.Equal("B", third.Parameters["Variant"]);
    }

    [Fact]
    public void Render_UnregisteredTagInPreview_SkipsWithWarning()
    {
        var md = "```razor:preview\n<Missing />\n```";
        var renderer = new MarkdownRenderer();
        var doc = renderer.Render(md);

        Assert.Empty(doc.Slots);
        Assert.NotEmpty(renderer.LastWarnings);
    }

    [Fact]
    public void Render_PlainHtmlWithoutTagsWeCareAbout_PassesThrough()
    {
        var doc = new MarkdownRenderer().Render("Text with <em>emphasis</em> and <strong>strong</strong>.");
        Assert.Empty(doc.Slots);
        Assert.Contains("<em>emphasis</em>", doc.Html);
    }

    [Fact]
    public void Render_PopulatesHeadings()
    {
        var doc = new MarkdownRenderer().Render("# Intro\n\n## Section\n\n### Detail");
        Assert.Equal(3, doc.Headings.Count);
        Assert.Equal("intro", doc.Headings[0].Id);
    }

    [Fact]
    public void Render_EmptyDocument_ReturnsEmptyShape()
    {
        var doc = new MarkdownRenderer().Render("");
        Assert.Equal("", doc.Html);
        Assert.Empty(doc.Slots);
        Assert.Empty(doc.Headings);
    }
}
