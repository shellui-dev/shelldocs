using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using ShellDocs.Components;
using ShellDocs.Components.Content;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class GenericComponentTests
{
    public class Widget { }

    // Renders the closed type argument so tests can see what it was closed over.
    public class Grid<TItem> : ComponentBase
    {
        [Parameter] public IEnumerable<TItem>? Items { get; set; }
        [Parameter] public string? Caption { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenElement(0, "table");
            b.AddAttribute(1, "data-item", typeof(TItem).Name);
            b.AddAttribute(2, "data-caption", Caption);
            b.CloseElement();
        }
    }

    public class StructOnly<T> : ComponentBase where T : struct
    {
        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenElement(0, "span");
            b.AddAttribute(1, "data-item", typeof(T).Name);
            b.CloseElement();
        }
    }

    public class Picker : ComponentBase { }
    public class Picker<T> : ComponentBase { }

    private static ComponentRenderHarness Harness() => new(o =>
    {
        o.ContentRoot = Path.Combine(Path.GetTempPath(), "shelldocs-no-content-" + Guid.NewGuid().ToString("N"));
        o.RegisterComponent(typeof(Grid<>));
        o.RegisterComponent(typeof(StructOnly<>));
    });

    private static Task<string> Render(ComponentRenderHarness harness, string markdown)
        => harness.RenderAsync<MarkdownContent>(new() { ["Markdown"] = markdown });

    [Fact]
    public void Registry_UsesTheBareName_ForGenericDefinitions()
    {
        Assert.Equal("Grid", TypeRegistry.TagNameOf(typeof(Grid<>)));
        Assert.Equal("Grid", TypeRegistry.TagNameOf(typeof(Grid<int>)));
        Assert.Equal(typeof(Grid<>), new TypeRegistry().Register(typeof(Grid<>)).Resolve("Grid"));
    }

    [Fact]
    public void Registry_PrefersTheNonGenericComponent_WhenBothExist()
    {
        var options = new ShellDocsOptions();
        options.RegisterComponent(typeof(Picker<>));
        options.RegisterComponent(typeof(Picker));
        var registry = options.BuildTypeRegistry();

        Assert.Equal(typeof(Picker), registry.Resolve("Picker"));
        Assert.Empty(registry.Collisions);
    }

    [Fact]
    public async Task RazorPreview_ClosesGenericFromTypeParameterAttribute()
    {
        var html = await Render(Harness(), "```razor:preview\n<Grid TItem=\"int\" Caption=\"Totals\" />\n```");

        Assert.Contains("data-item=\"Int32\"", html);
        Assert.Contains("data-caption=\"Totals\"", html);
        Assert.DoesNotContain("shelldocs-render-error", html);
    }

    [Fact]
    public async Task InlineTag_InProse_ClosesGenericToo()
    {
        var html = await Render(Harness(), "Text.\n\n<Grid TItem=\"ShellDocs.Tests.GenericComponentTests+Widget\" />\n");
        Assert.Contains("data-item=\"Widget\"", html);
    }

    [Fact]
    public async Task MissingTypeArgument_FallsBackToObject_WithAWarning()
    {
        var harness = Harness();
        var html = await Render(harness, "```razor:preview\n<Grid />\n```");

        Assert.Contains("data-item=\"Object\"", html);
        Assert.Contains(harness.Logs.Messages, m => m.Contains("no TItem=") && m.Contains("using object"));
    }

    [Fact]
    public async Task ConstraintOrUnknownType_RendersAVisibleError()
    {
        var constrained = await Render(Harness(), "```razor:preview\n<StructOnly />\n```");
        Assert.Contains("shelldocs-render-error", constrained);
        Assert.Contains("Set T=", constrained);

        var unknown = await Render(Harness(), "```razor:preview\n<Grid TItem=\"No.Such.Type\" />\n```");
        Assert.Contains("shelldocs-render-error", unknown);
        Assert.Contains("can&#x27;t resolve TItem", unknown);
    }

    [Fact]
    public async Task ComponentPreview_ClosesGenericTarget()
    {
        var html = await Harness().RenderAsync<ComponentPreview>(new()
        {
            ["Component"] = "Grid",
            ["ExtraProps"] = new Dictionary<string, object> { ["TItem"] = "decimal", ["Caption"] = "Prices" }
        });

        Assert.Contains("data-item=\"Decimal\"", html);
        Assert.Contains("data-caption=\"Prices\"", html);
        Assert.Contains("id=\"example-grid-", html);
    }

    [Theory]
    [InlineData("int", typeof(int))]
    [InlineData("int?", typeof(int?))]
    [InlineData("string[]", typeof(string[]))]
    [InlineData("List<int>", typeof(List<int>))]
    [InlineData("Dictionary<string, List<int>>", typeof(Dictionary<string, List<int>>))]
    [InlineData("DateTime", typeof(DateTime))]
    [InlineData("System.Guid", typeof(Guid))]
    [InlineData("@double", typeof(double))]
    public void ResolveTypeName_HandlesCSharpSpellings(string name, Type expected)
        => Assert.Equal(expected, GenericComponents.ResolveTypeName(name));

    [Fact]
    public async Task AutoTypeTable_ListsTypeParameters_ForGenericComponents()
    {
        var html = await Harness().RenderAsync<AutoTypeTable>(new() { ["Component"] = "Grid" });

        Assert.Contains("<code>TItem</code>", html);
        Assert.Contains("type parameter", html);
        Assert.Contains("IEnumerable&lt;TItem&gt;?", html);
    }
}
