using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using ShellDocs.Components;
using ShellDocs.Components.Content;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public enum ButtonVariant { Default, Destructive, Outline }

[Flags]
public enum TextStyle { None = 0, Bold = 1, Italic = 2, Underline = 4 }

public class Button : ComponentBase
{
    [Parameter] public ButtonVariant Variant { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public int Size { get; set; }
    [Parameter] public double? Ratio { get; set; }
    [Parameter] public TextStyle Style { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public EventCallback<int> OnCount { get; set; }
    [Parameter] public Action? OnAction { get; set; }
    [Parameter] public Func<int, int>? Transform { get; set; }
    [Parameter] public List<string>? Items { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenElement(0, "button");
        b.AddAttribute(1, "data-variant", Variant.ToString());
        b.AddAttribute(2, "data-style", Style.ToString());
        b.AddAttribute(3, "data-size", Size);
        b.AddAttribute(4, "disabled", Disabled);
        b.AddContent(5, ChildContent);
        b.CloseElement();
    }
}

// Has a catch-all, so unknown attributes pass straight through.
public class Badge : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? Attrs { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenElement(0, "span");
        b.AddMultipleAttributes(1, Attrs);
        b.AddContent(2, ChildContent);
        b.CloseElement();
    }
}

public class PreviewParsingTests
{
    private static MarkdownRenderer Renderer() => new(new TypeRegistry().Register<Button>().Register<Badge>());

    private static PreviewSlot Preview(string fence)
    {
        var doc = Renderer().Render("```razor:preview\n" + fence + "\n```");
        return Assert.IsType<PreviewSlot>(Assert.Single(doc.Slots));
    }

    [Fact]
    public void MultipleTopLevelSiblings_AllBecomeNodes_InOrder()
    {
        var slot = Preview("<Button>One</Button>\n<Button Variant=\"Outline\">Two</Button>\n<Button>Three</Button>");

        var comps = slot.Nodes!.OfType<PreviewComponentNode>().ToList();
        Assert.Equal(3, comps.Count);
        Assert.Equal(new[] { "One", "Two", "Three" }, comps.Select(c => c.ChildContentRaw));
        Assert.Equal("Outline", comps[1].Parameters["Variant"]);
        // First component still drives the legacy single-component fields.
        Assert.Equal(typeof(Button), slot.ComponentType);
        Assert.Equal("One", slot.ChildContentRaw);
    }

    [Fact]
    public void HtmlWrapperAndText_ArePreserved()
    {
        var slot = Preview("<div class=\"flex gap-2\">\n  <Button>A</Button>\n  <Button>B</Button>\n</div>\n<p>Some &amp; text</p>");

        var wrapper = Assert.IsType<PreviewElementNode>(slot.Nodes!.First(n => n is PreviewElementNode));
        Assert.Equal("div", wrapper.TagName);
        Assert.Equal("flex gap-2", wrapper.Attributes.Single(a => a.Key == "class").Value);
        Assert.Equal(2, wrapper.Children.OfType<PreviewComponentNode>().Count());

        var p = slot.Nodes!.OfType<PreviewElementNode>().Single(n => n.TagName == "p");
        Assert.Equal("Some & text", Assert.IsType<PreviewTextNode>(Assert.Single(p.Children)).Text);
    }

    [Fact]
    public void SourceView_IsWholeFence()
    {
        var fence = "<Button>A</Button>\n<Button>B</Button>";
        Assert.Equal(fence, Preview(fence).Code);
    }

    [Fact]
    public void RazorAttributes_KeepFullNames_AndLambdasDoNotBreakTheTag()
    {
        var slot = Preview("<Button @bind-Value=\"v\" @ref=\"btn\" OnClick=\"@(() => Log(\"x > y\"))\" Variant=\"ButtonVariant.Destructive\">Go</Button>");
        var comp = Assert.IsType<PreviewComponentNode>(Assert.Single(slot.Nodes!));
        Assert.Equal("v", comp.Parameters["@bind-Value"]);
        Assert.Equal("btn", comp.Parameters["@ref"]);
        Assert.Equal("@(() => Log(\"x > y\"))", comp.Parameters["OnClick"]);
        Assert.Equal("ButtonVariant.Destructive", comp.Parameters["Variant"]);
        Assert.False(comp.Parameters.ContainsKey("Value"));
        Assert.Equal("Go", comp.ChildContentRaw);
    }

    [Fact]
    public void RazorOnlyConstructs_AreSkippedForRendering_ButKeptInSource()
    {
        var fence = "@using Foo.Bar\n@* note *@\n<Button>A</Button>\n@code {\n    int count = 0; void F() { if (true) { } }\n}";
        var slot = Preview(fence);
        Assert.Single(slot.Nodes!.OfType<PreviewComponentNode>());
        Assert.DoesNotContain(slot.Nodes!.OfType<PreviewTextNode>(), t => t.Text.Contains("count") || t.Text.Contains("@using") || t.Text.Contains("note"));
        Assert.Contains("@code", slot.Code);
    }

    [Fact]
    public void ElementDirectiveAttributes_AreDropped()
    {
        var slot = Preview("<div @onclick=\"Go\" class=\"x\" hidden><Button /></div>");
        var div = Assert.IsType<PreviewElementNode>(Assert.Single(slot.Nodes!));
        Assert.DoesNotContain(div.Attributes, a => a.Key.StartsWith('@'));
        Assert.Null(div.Attributes.Single(a => a.Key == "hidden").Value);
    }

    [Fact]
    public void OnlyUnknownComponents_KeepsErrorPanel()
    {
        var renderer = Renderer();
        var doc = renderer.Render("```razor:preview\n<div><Missing Foo=\"1\">x</Missing></div>\n```");
        var slot = Assert.IsType<PreviewSlot>(Assert.Single(doc.Slots));
        Assert.Null(slot.ComponentType);
        Assert.Contains("Unknown component <Missing>", slot.Error);
        Assert.Equal("1", slot.Parameters["Foo"]);
        Assert.Equal("x", slot.ChildContentRaw);
        Assert.Contains(renderer.LastWarnings, w => w.Contains("Missing"));
    }

    [Fact]
    public void KnownAndUnknownSiblings_RenderKnown_WarnUnknown()
    {
        var renderer = Renderer();
        var doc = renderer.Render("```razor:preview\n<Button>A</Button>\n<Missing />\n```");
        var slot = Assert.IsType<PreviewSlot>(Assert.Single(doc.Slots));
        Assert.Null(slot.Error);
        Assert.Contains(slot.Nodes!, n => n is PreviewElementNode { TagName: "Missing" });
        Assert.Contains(renderer.LastWarnings, w => w.Contains("Missing"));
    }

    [Fact]
    public void FenceWithoutComponentTags_RendersAsPlainCode()
    {
        var renderer = Renderer();
        var doc = renderer.Render("```razor:preview\n<div>plain</div>\n```");
        Assert.Empty(doc.Slots);
        Assert.Contains("language-razor:preview", doc.Html);
        Assert.Contains(renderer.LastWarnings, w => w.Contains("must contain a component tag"));
    }

    [Fact]
    public void InlineComponentTag_BindAttribute_IsNotMisreadAsParameter()
    {
        var doc = Renderer().Render("<Button @bind-Size=\"n\" Size=\"3\" />");
        var slot = Assert.IsType<ComponentSlot>(Assert.Single(doc.Slots));
        Assert.Equal("3", slot.Parameters["Size"]);
        Assert.Equal("n", slot.Parameters["@bind-Size"]);
    }
}

public class SlotRendererCoercionTests
{
    [Theory]
    [InlineData("Destructive")]
    [InlineData("destructive")]
    [InlineData("ButtonVariant.Destructive")]
    [InlineData("@ButtonVariant.Destructive")]
    [InlineData("ShellDocs.Tests.ButtonVariant.Destructive")]
    [InlineData("@(ButtonVariant.Destructive)")]
    public void Enum_AcceptsRazorForms(string raw)
        => Assert.Equal(ButtonVariant.Destructive, SlotRenderer.Coerce(raw, typeof(ButtonVariant)));

    [Theory]
    [InlineData("Bold | Italic")]
    [InlineData("TextStyle.Bold|TextStyle.Italic")]
    [InlineData("@(TextStyle.Bold | TextStyle.Italic)")]
    [InlineData("Bold, Italic")]
    public void FlagsEnum_AcceptsPipe(string raw)
        => Assert.Equal(TextStyle.Bold | TextStyle.Italic, SlotRenderer.Coerce(raw, typeof(TextStyle)));

    [Fact]
    public void Primitives_AcceptLeadingAt()
    {
        Assert.Equal(true, SlotRenderer.Coerce("@true", typeof(bool)));
        Assert.Equal(true, SlotRenderer.Coerce("", typeof(bool))); // bare attribute
        Assert.Equal(42, SlotRenderer.Coerce("@42", typeof(int)));
        Assert.Equal(1.5, SlotRenderer.Coerce("@1.5", typeof(double)));
        Assert.Equal(10m, SlotRenderer.Coerce("10m", typeof(decimal)));
        Assert.Equal(2.5f, SlotRenderer.Coerce("2.5f", typeof(float)));
        Assert.Equal(7, SlotRenderer.Coerce("7", typeof(int?)));
        Assert.Null(SlotRenderer.Coerce("@null", typeof(int?)));
        Assert.Equal("@literal", SlotRenderer.Coerce("@literal", typeof(string)));
    }

    [Theory]
    [InlineData("Nope", typeof(ButtonVariant))]
    [InlineData("maybe", typeof(bool))]
    [InlineData("abc", typeof(int))]
    [InlineData("99999999999", typeof(int))]
    [InlineData("x", typeof(List<string>))]
    public void BadValues_Throw_ButTryCoerceReturnsFalse(string raw, Type type)
    {
        Assert.ThrowsAny<Exception>(() => SlotRenderer.Coerce(raw, type));
        Assert.False(SlotRenderer.TryCoerce(raw, type, out _));
    }
}

public class SlotRendererSafetyTests
{
    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Messages.Add(formatter(state, exception));
        }
    }

    private static (IDictionary<string, object> Params, ListLogger Log) Build(Type t, Dictionary<string, string> attrs, string? child = null)
    {
        var log = new ListLogger();
        return (SlotRenderer.BuildParameters(new MarkdownRenderer(), t, attrs, child, log), log);
    }

    [Fact]
    public void UncoercibleAttributes_AreSkippedAndLogged_RestStillApplies()
    {
        var (p, log) = Build(typeof(Button), new()
        {
            ["OnClick"] = "HandleClick",
            ["OnCount"] = "Count",
            ["OnAction"] = "Go",
            ["Transform"] = "x => x",
            ["Items"] = "a,b",
            ["@bind-Size"] = "n",
            ["@ref"] = "btn",
            ["@onclick"] = "Go",
            ["Size"] = "nope",
            ["Variant"] = "ButtonVariant.Destructive",
            ["Disabled"] = "@true",
        });

        Assert.Equal(ButtonVariant.Destructive, p["Variant"]);
        Assert.Equal(true, p["Disabled"]);
        foreach (var skipped in new[] { "OnClick", "OnCount", "OnAction", "Transform", "Items", "@bind-Size", "@ref", "@onclick", "Size" })
        {
            Assert.False(p.ContainsKey(skipped), skipped);
            Assert.Contains(log.Messages, m => m.Contains($"'{skipped}'"));
        }
    }

    [Fact]
    public void UnknownAttribute_WithoutCatchAll_IsSkipped_WithCatchAll_PassesThrough()
    {
        var (p1, log1) = Build(typeof(Button), new() { ["class"] = "x" });
        Assert.False(p1.ContainsKey("class"));
        Assert.Contains(log1.Messages, m => m.Contains("'class'"));

        var (p2, _) = Build(typeof(Badge), new() { ["class"] = "x", ["@onclick"] = "Go" });
        Assert.Equal("x", p2["class"]);
        Assert.False(p2.ContainsKey("@onclick"));
    }

    [Fact]
    public void ParameterNames_MatchCaseInsensitively()
    {
        var (p, _) = Build(typeof(Button), new() { ["variant"] = "Outline" });
        Assert.Equal(ButtonVariant.Outline, p["Variant"]);
    }

    [Fact]
    public void ChildContent_OnComponentWithoutChildContent_IsSkipped()
    {
        var (p, log) = Build(typeof(NoChildren), new(), "text");
        Assert.Empty(p);
        Assert.Contains(log.Messages, m => m.Contains("ChildContent"));
    }

    public class NoChildren : ComponentBase { }
}

public class PreviewRenderingTests : IDisposable
{
    private readonly string _demoRoot;

    public PreviewRenderingTests()
    {
        _demoRoot = Path.Combine(Path.GetTempPath(), "shelldocs-demos-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_demoRoot, "Buttons"));
        File.WriteAllText(Path.Combine(_demoRoot, "Buttons", "ButtonClickDemo.razor"),
            "<Button OnClick=\"Inc\">Clicked @count</Button>\n@code { int count; void Inc() => count++; }");
    }

    public void Dispose()
    {
        try { Directory.Delete(_demoRoot, recursive: true); } catch { }
    }

    private ComponentRenderHarness Harness() => new(o =>
    {
        o.ContentRoot = Path.Combine(_demoRoot, "no-content");
        o.DemoSourceRoot = _demoRoot;
        o.RegisterComponent<Button>();
        o.RegisterComponent<ButtonClickDemo>();
    });

    private static Dictionary<string, object?> Md(string markdown) => new() { ["Markdown"] = markdown };

    public class ButtonClickDemo : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenElement(0, "span");
            b.AddAttribute(1, "class", "demo-output");
            b.AddContent(2, "Clicked 0");
            b.CloseElement();
        }
    }

    [Fact]
    public async Task MultiSiblingPreview_RendersEveryComponent_InsideWrapper_InOrder()
    {
        var html = await Harness().RenderAsync<MarkdownContent>(Md(
            "```razor:preview\n<div class=\"flex gap-2\">\n<Button>One</Button>\n<Button Variant=\"ButtonVariant.Destructive\">Two</Button>\n</div>\n<Button Variant=\"@ButtonVariant.Outline\">Three</Button>\n```"));

        var one = html.IndexOf(">One", StringComparison.Ordinal);
        var two = html.IndexOf(">Two", StringComparison.Ordinal);
        var three = html.IndexOf(">Three", StringComparison.Ordinal);
        Assert.True(one > 0 && two > one && three > two, html);
        Assert.Contains("data-variant=\"Destructive\"", html);
        Assert.Contains("data-variant=\"Outline\"", html);

        // The wrapper is a real element enclosing the first two buttons.
        var wrapStart = html.IndexOf("<div class=\"flex gap-2\">", StringComparison.Ordinal);
        var wrapEnd = html.IndexOf("</div>", two, StringComparison.Ordinal);
        Assert.True(wrapStart > 0 && wrapStart < one && wrapEnd < three, html);
    }

    [Fact]
    public async Task UncoercibleAttributes_DoNotCrash_ComponentStillRenders()
    {
        var harness = Harness();
        var html = await harness.RenderAsync<MarkdownContent>(Md(
            "```razor:preview\n<Button OnClick=\"HandleClick\" @onclick=\"X\" @bind-Size=\"s\" @ref=\"b\" Items=\"a\" Size=\"huge\" Variant=\"Bogus\">Safe</Button>\n```"));

        Assert.Contains(">Safe", html);
        Assert.Contains("<button data-variant=", html);
        Assert.Contains("data-variant=\"Default\"", html);
        Assert.Contains(harness.Logs.Messages, m => m.StartsWith("Warning") && m.Contains("'OnClick'"));
        Assert.Contains(harness.Logs.Messages, m => m.Contains("'Variant'"));
    }

    [Fact]
    public async Task UnknownComponentPreview_RendersErrorPanel()
    {
        var html = await Harness().RenderAsync<MarkdownContent>(Md("```razor:preview\n<Missing />\n```"));
        Assert.Contains("preview-error", html);
        Assert.Contains("Unknown component &lt;Missing&gt;", html);
    }

    [Fact]
    public async Task DemoPreview_RendersComponent_AndRazorSourceFromDisk()
    {
        var html = await Harness().RenderAsync<DemoPreview>(new() { ["Component"] = "ButtonClickDemo", ["Title"] = "Click demo" });

        Assert.Contains("class=\"demo-output\"", html);
        Assert.Contains("preview-title", html);
        Assert.Contains("Click demo", html);
        Assert.Contains("language-razor", html);
        Assert.Contains("@code { int count; void Inc() =&gt; count&#x2B;&#x2B;; }", html);
        Assert.DoesNotContain("preview-error", html);
    }

    [Fact]
    public async Task DemoPreview_IsUsableInlineInMarkdown()
    {
        var html = await Harness().RenderAsync<MarkdownContent>(Md("Intro.\n\n<DemoPreview Component=\"ButtonClickDemo\" />\n\nOutro."));
        Assert.Contains("class=\"demo-output\"", html);
        Assert.Contains("Clicked @count", html);
    }

    [Fact]
    public async Task DemoPreview_MissingComponent_RendersErrorPanel()
    {
        var html = await Harness().RenderAsync<DemoPreview>(new() { ["Component"] = "NopeDemo" });
        Assert.Contains("preview-error", html);
        Assert.Contains("DemoPreview error", html);
        Assert.Contains("Unknown demo component", html);
    }

    [Fact]
    public async Task DemoPreview_MissingFile_RendersErrorPanel_WithoutLeakingPath()
    {
        File.Delete(Path.Combine(_demoRoot, "Buttons", "ButtonClickDemo.razor"));
        var html = await Harness().RenderAsync<DemoPreview>(new() { ["Component"] = "ButtonClickDemo" });
        Assert.Contains("preview-error", html);
        Assert.Contains("Demo source not found", html);
        Assert.DoesNotContain(_demoRoot, html);
    }

    [Fact]
    public void DemoSourceCache_PrefersShallowestMatch_AndPicksUpEdits()
    {
        File.WriteAllText(Path.Combine(_demoRoot, "ButtonClickDemo.razor"), "top-level");
        Assert.Equal("top-level", DemoSourceCache.Get(_demoRoot, "ButtonClickDemo"));

        var path = Path.Combine(_demoRoot, "ButtonClickDemo.razor");
        File.WriteAllText(path, "edited");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        Assert.Equal("edited", DemoSourceCache.Get(_demoRoot, "ButtonClickDemo"));
        Assert.Null(DemoSourceCache.Get(_demoRoot, "Nothing"));
    }
}
