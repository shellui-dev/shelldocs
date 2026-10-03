using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using ShellDocs.Components;
using ShellDocs.Components.Chrome;
using ShellDocs.Components.Content;
using Xunit;

namespace ShellDocs.Tests;

public class PreviewFidelityTests
{
    public class Panel : ComponentBase
    {
        [Parameter] public RenderFragment? ChildContent { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenElement(0, "nav");
            b.AddContent(1, ChildContent);
            b.CloseElement();
        }
    }

    private static ComponentRenderHarness Harness() => new(o =>
    {
        o.ContentRoot = Path.Combine(Path.GetTempPath(), "shelldocs-no-content-" + Guid.NewGuid().ToString("N"));
        o.RegisterComponent<Button>();
        o.RegisterComponent<Badge>();
        o.RegisterComponent<Panel>();
    });

    private static Task<string> Render(string markdown)
        => Harness().RenderAsync<MarkdownContent>(new() { ["Markdown"] = markdown });

    [Fact]
    public async Task RazorPreview_ChildContent_KeepsNestedMarkupAroundComponents()
    {
        var html = await Render("""
            ```razor:preview
            <Panel>
                <a href="/" class="font-bold">MyApp</a>
                <div class="flex items-center gap-4"><a href="/docs">Docs</a><Button>Go</Button></div>
            </Panel>
            ```
            """);

        var nav = Regex.Match(html, "<nav>(.*?)</nav>", RegexOptions.Singleline).Groups[1].Value;
        Assert.DoesNotContain("<p>", nav);
        Assert.Contains("<a href=\"/\" class=\"font-bold\">MyApp</a>", nav);
        Assert.Matches("<div class=\"flex items-center gap-4\"><a href=\"/docs\">Docs</a><button[^>]*>Go</button></div>", nav);
    }

    [Fact]
    public async Task RazorPreview_NestedComponents_AreRazorAllTheWayDown()
    {
        var html = await Render("""
            ```razor:preview
            <Panel>
                <section><Panel><div class="inner"><Button>Deep</Button></div></Panel></section>
            </Panel>
            ```
            """);

        Assert.Matches("<section><nav>\\s*<div class=\"inner\"><button[^>]*>Deep</button></div>\\s*</nav></section>", html);
        Assert.DoesNotContain("<p>", Regex.Match(html, "<nav>.*</nav>", RegexOptions.Singleline).Value);
    }

    [Fact]
    public async Task InlineComponent_InProse_StillTakesMarkdownChildren()
    {
        var html = await Render("Some text.\n\n<Badge>**bold** body</Badge>\n");

        Assert.Contains("<strong>bold</strong>", html);
    }

    [Fact]
    public async Task ComponentPreview_ChildContent_IsParsedAsRazor()
    {
        var html = await Render("<ComponentPreview Component=\"Panel\">\n<div class=\"row\"><Button>X</Button></div>\n</ComponentPreview>\n");

        // HtmlRenderer encodes the newline text nodes around the div as &#xA;.
        Assert.Matches("<nav>(\\s|&#xA;)*<div class=\"row\"><button[^>]*>X</button></div>(\\s|&#xA;)*</nav>", html);
    }

    [Fact]
    public async Task PreviewLayout_DefaultsToCenter_StretchOrScrollFromFenceOrParameter()
    {
        var centered = await Render("```razor:preview\n<Button>A</Button>\n```");
        Assert.Contains("data-layout=\"center\"", centered);

        var stretched = await Render("```razor:preview stretch\n<Button>A</Button>\n```");
        Assert.Contains("data-layout=\"stretch\"", stretched);

        var component = await Harness().RenderAsync<ComponentPreview>(new() { ["Component"] = "Button", ["Layout"] = "stretch" });
        Assert.Contains("data-layout=\"stretch\"", component);

        var scrolled = await Render("```razor:preview scroll\n<Button>A</Button>\n```");
        Assert.Contains("data-layout=\"scroll\"", scrolled);

        var unknown = await Harness().RenderAsync<ComponentPreview>(new() { ["Component"] = "Button", ["Layout"] = "sideways" });
        Assert.Contains("data-layout=\"center\"", unknown);
    }

    [Fact]
    public async Task PreviewFrame_OptsOutOfProse()
    {
        var html = await Render("```razor:preview\n<Button>A</Button>\n```");
        Assert.Contains("class=\"preview-frame not-prose\"", html);
    }

    [Fact]
    public void ProseRules_AllSkipNotProseSubtrees()
    {
        var css = ReadThemeCss();
        var selectors = Regex.Matches(css, @"^(\.shelldocs-prose [^{]+)\{", RegexOptions.Multiline)
            .SelectMany(m => Regex.Split(m.Groups[1].Value, @",(?![^(]*\))"))
            .Select(s => s.Trim())
            .Where(s => s.StartsWith(".shelldocs-prose ") && !s.StartsWith(".shelldocs-prose >") && !s.Contains("pre.shiki"))
            .ToList();

        Assert.NotEmpty(selectors);
        Assert.All(selectors, s => Assert.Contains(":where(:not(.not-prose, .not-prose *))", s));
    }

    [Fact]
    public async Task ThemeToggle_IsStaticHostFriendly()
    {
        var html = await Harness().RenderAsync<ThemeToggle>();

        Assert.Contains("data-theme-toggle", html);
        Assert.Contains("theme-icon-light", html);
        Assert.Contains("theme-icon-dark", html);
        Assert.DoesNotContain("blazor:onclick", html);
    }

    private static string ReadThemeCss()
    {
        var testDir = Path.GetDirectoryName(typeof(PreviewFidelityTests).Assembly.Location)!;
        var candidates = new[]
        {
            Path.Combine(testDir, "wwwroot", "_content", "ShellDocs.Components", "shelldocs-theme.css"),
            Path.Combine(testDir, "..", "..", "..", "..", "..", "src", "ShellDocs.Components", "wwwroot", "shelldocs-theme.css")
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full)) return File.ReadAllText(full);
        }
        throw new FileNotFoundException("shelldocs-theme.css not found");
    }
}
