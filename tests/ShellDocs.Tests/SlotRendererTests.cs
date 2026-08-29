using System.Reflection;
using Microsoft.AspNetCore.Components;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

// Named-slot routing behavior: child tags matching a target component's
// [Parameter] RenderFragment prop names should route into that param instead
// of being flattened into ChildContent.
public class SlotRendererTests
{
    public class Alert : ComponentBase
    {
        [Parameter] public string? Title { get; set; }
        [Parameter] public RenderFragment? Icon { get; set; }
        [Parameter] public RenderFragment? Footer { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
    }

    private static readonly MethodInfo BuildParametersMethod = LoadBuildParameters();

    private static MethodInfo LoadBuildParameters()
    {
        var asm = Assembly.Load("ShellDocs.Components");
        var t = asm.GetType("ShellDocs.Components.Content.SlotRenderer", throwOnError: true)!;
        return t.GetMethod("BuildParameters", BindingFlags.Public | BindingFlags.Static)!;
    }

    private static IDictionary<string, object> BuildParameters(Type target, IReadOnlyDictionary<string, string> attrs, string? childRaw)
    {
        var renderer = new MarkdownRenderer();
        return (IDictionary<string, object>)BuildParametersMethod.Invoke(null, new object?[] { renderer, target, attrs, childRaw })!;
    }

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
}
