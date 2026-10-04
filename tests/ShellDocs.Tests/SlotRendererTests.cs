using Microsoft.AspNetCore.Components;
using ShellDocs.Components.Content;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

// Child tags named after a RenderFragment parameter route into it, not ChildContent.
public class SlotRendererTests
{
    public class Alert : ComponentBase
    {
        [Parameter] public string? Title { get; set; }
        [Parameter] public RenderFragment? Icon { get; set; }
        [Parameter] public RenderFragment? Footer { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
    }

    private static IDictionary<string, object> BuildParameters(Type target, IReadOnlyDictionary<string, string> attrs, string? childRaw)
        => SlotRenderer.BuildParameters(new MarkdownRenderer(), target, attrs, childRaw);

    [Fact]
    public void BuildParameters_RoutesNamedSlotIntoRenderFragmentParam()
    {
        var attrs = new Dictionary<string, string> { ["Title"] = "Heads up" };
        var raw = "<Icon><svg></svg></Icon>Body text.";

        var dict = BuildParameters(typeof(Alert), attrs, raw);

        Assert.Equal("Heads up", dict["Title"]);
        Assert.IsType<RenderFragment>(dict["Icon"]);
        Assert.IsType<RenderFragment>(dict["ChildContent"]);
        Assert.False(dict.ContainsKey("Footer"));
    }

    [Fact]
    public void BuildParameters_RoutesMultipleNamedSlots()
    {
        var raw = "<Icon><svg/></Icon>Middle text<Footer>Small print</Footer>";
        var dict = BuildParameters(typeof(Alert), new Dictionary<string, string>(), raw);

        Assert.IsType<RenderFragment>(dict["Icon"]);
        Assert.IsType<RenderFragment>(dict["Footer"]);
        Assert.IsType<RenderFragment>(dict["ChildContent"]);
    }

    [Fact]
    public void BuildParameters_ChildContentOnly_WhenNoNamedSlotTagsPresent()
    {
        var dict = BuildParameters(typeof(Alert), new Dictionary<string, string>(), "Just body text.");

        Assert.IsType<RenderFragment>(dict["ChildContent"]);
        Assert.False(dict.ContainsKey("Icon"));
        Assert.False(dict.ContainsKey("Footer"));
    }

    [Fact]
    public void BuildParameters_NoChildContent_WhenAllRawIsConsumedByNamedSlots()
    {
        var raw = "<Icon><svg/></Icon><Footer>Only slots.</Footer>";
        var dict = BuildParameters(typeof(Alert), new Dictionary<string, string>(), raw);

        Assert.IsType<RenderFragment>(dict["Icon"]);
        Assert.IsType<RenderFragment>(dict["Footer"]);
        Assert.False(dict.ContainsKey("ChildContent"));
    }

    [Fact]
    public void BuildParameters_SelfClosingNamedSlot_Extracted()
    {
        var raw = "<Icon />Body.";
        var dict = BuildParameters(typeof(Alert), new Dictionary<string, string>(), raw);

        Assert.IsType<RenderFragment>(dict["Icon"]);
        Assert.IsType<RenderFragment>(dict["ChildContent"]);
    }

    [Fact]
    public void ExtractNamedSlot_AngleBracketInAnAttributeValue_DoesNotEndTheTag()
    {
        var (content, remaining) = SlotRenderer.ExtractNamedSlot("<Icon title=\"a > b\"><svg/></Icon>Body.", "Icon");

        Assert.Equal("<svg/>", content);
        Assert.Equal("Body.", remaining);
    }

    [Fact]
    public void ExtractNamedSlot_NestedTagOfTheSameName_IsBalanced()
    {
        var (content, remaining) = SlotRenderer.ExtractNamedSlot(
            "Lead <Footer>a <Footer note=\"x > y\" /> b <Footer>c</Footer></Footer> tail", "Footer");

        Assert.Equal("a <Footer note=\"x > y\" /> b <Footer>c</Footer>", content);
        Assert.Equal("Lead  tail", remaining);
    }

    [Fact]
    public void ExtractNamedSlot_OtherTagsAndUnclosedSlots_AreLeftAlone()
    {
        Assert.Equal((null, "<Icons>x</Icons>"), SlotRenderer.ExtractNamedSlot("<Icons>x</Icons>", "Icon"));
        Assert.Equal((null, "<Icon>never closed"), SlotRenderer.ExtractNamedSlot("<Icon>never closed", "Icon"));
    }
}
