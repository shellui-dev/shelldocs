using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using ShellDocs.Components;
using ShellDocs.Components.Chrome;
using ShellDocs.Components.Content;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class CodeSpanMaskingTests
{
    public class Callout { }
    public class Button { }

    private static MarkdownRenderer Renderer() => new(new TypeRegistry().Register<Callout>().Register<Button>());

    [Theory]
    [InlineData("Use `<Button>` for actions.")]
    [InlineData("Use `<Button />` for actions.")]
    [InlineData("Use ``<Button>` with a tick`` here.")]
    [InlineData("Wrapped `<Button\nVariant=\"x\" />` span.")]
    public void TagsInsideCodeSpans_StayLiteral(string md)
    {
        var doc = Renderer().Render(md);
        Assert.Empty(doc.Slots);
        Assert.Contains("<code>", doc.Html);
        Assert.Contains("&lt;Button", doc.Html);
        Assert.DoesNotContain("SHELLDOCS_MASK", doc.Html);
    }

    [Fact]
    public void UnclosedBacktick_DoesNotSwallowTags()
    {
        var doc = Renderer().Render("A stray ` tick, then <Button />.");
        Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
    }

    [Fact]
    public void CodeSpan_DoesNotCrossBlankLine()
    {
        var doc = Renderer().Render("Start `open\n\n<Button />\n\nend` here.");
        Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
    }

    [Fact]
    public void ComponentChildMarkup_GetsCodeSpansAndFencesBack()
    {
        var doc = Renderer().Render("<Callout>\nUse `<Button>` here.\n\n```js\nconst a = '<Button />';\n```\n</Callout>");
        var slot = Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
        Assert.Contains("`<Button>`", slot.ChildContentRaw);
        Assert.Contains("```js", slot.ChildContentRaw);
        Assert.DoesNotContain("SHELLDOCS_MASK", slot.ChildContentRaw);
    }

    [Fact]
    public void CodeSpanInAttributeValue_IsRestored()
    {
        var doc = Renderer().Render("<Callout Title=\"Use `x`\">Body</Callout>");
        var slot = Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
        Assert.Equal("Use `x`", slot.Parameters["Title"]);
    }
}
