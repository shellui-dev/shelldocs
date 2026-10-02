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

public class PreviewToolbarTests
{
    private static ComponentRenderHarness Harness(Action<ShellDocsOptions>? extra = null, string uri = "http://localhost/docs/button") => new(o =>
    {
        o.ContentRoot = Path.Combine(Path.GetTempPath(), "shelldocs-no-content-" + Guid.NewGuid().ToString("N"));
        o.RegisterComponent<Button>();
        extra?.Invoke(o);
    }, uri);

    private static Dictionary<string, object?> Md(string markdown) => new() { ["Markdown"] = markdown };

    private const string TwoPreviews = "```razor:preview\n<Button>One</Button>\n```\n\nText.\n\n```razor:preview\n<Button>Two</Button>\n```";

    [Fact]
    public async Task Frames_RenderTabsAndPanels_WithOrdinalIds()
    {
        var html = await Harness().RenderAsync<MarkdownContent>(Md(TwoPreviews));

        Assert.Contains("id=\"preview-1\"", html);
        Assert.Contains("id=\"preview-2\"", html);
        Assert.Contains("data-preview-tab=\"preview\"", html);
        Assert.Contains("role=\"tablist\"", html);
        Assert.Contains("id=\"preview-1-tab-code\"", html);
        Assert.Contains("aria-controls=\"preview-1-panel-code\"", html);
        Assert.Contains("data-preview-tab-target=\"code\" aria-selected=\"false\" tabindex=\"-1\"", html);
        Assert.Contains("data-preview-tab-target=\"preview\" aria-selected=\"true\" tabindex=\"0\"", html);
        Assert.Contains("data-preview-panel=\"code\"", html);
        Assert.Contains("&lt;Button&gt;One&lt;/Button&gt;", html); // code panel is server-rendered too
        Assert.DoesNotContain("data-preview-toggle", html);
    }

    [Fact]
    public async Task Menu_LinksToAnchor_AndPrefilledGitHubIssues()
    {
        var html = await Harness(o =>
        {
            o.GitHubRepo = "acme/docs";
            o.SiteUrl = "https://docs.acme.dev/";
        }).RenderAsync<MarkdownContent>(Md(TwoPreviews));

        Assert.Contains("href=\"/docs/button#preview-1\"", html);
        Assert.Contains("Open in new tab", html);
        Assert.Contains("https://github.com/acme/docs/issues/new?title=Bug%20in%20the%20Button%20example", html);
        Assert.Contains("labels=bug", html);
        Assert.Contains("labels=enhancement", html);
        Assert.Contains("https%3A%2F%2Fdocs.acme.dev%2Fdocs%2Fbutton%23preview-2", html);
        Assert.Contains("Report a bug", html);
        Assert.Contains("Suggest something", html);
    }

    [Fact]
    public async Task Menu_CustomTracker_AppendsQuery_WithoutGitHubLabels()
    {
        var html = await Harness(o => o.IssueTrackerUrl = "https://example.com/feedback?src=docs")
            .RenderAsync<MarkdownContent>(Md(TwoPreviews));

        Assert.Contains("https://example.com/feedback?src=docs&amp;title=", html);
        Assert.DoesNotContain("labels=", html);
    }

    [Fact]
    public async Task Menu_WithoutTracker_HidesIssueItems()
    {
        var html = await Harness().RenderAsync<MarkdownContent>(Md(TwoPreviews));
        Assert.Contains("Open in new tab", html);
        Assert.DoesNotContain("Report a bug", html);
        Assert.DoesNotContain("Suggest something", html);
    }

    [Fact]
    public async Task ComponentPreview_RendersThroughPreviewFrame()
    {
        var harness = Harness();
        var html = await harness.RenderAsync<ComponentPreview>(new()
        {
            ["Component"] = "Button",
            ["ExtraProps"] = new Dictionary<string, object> { ["Variant"] = "Outline" }
        });

        Assert.Contains("class=\"preview-frame not-prose\"", html);
        Assert.Contains("id=\"example-button-", html);
        Assert.Contains("data-variant=\"Outline\"", html);
        Assert.Contains("&lt;Button Variant=&quot;Outline&quot; /&gt;", html);

        var unknown = await harness.RenderAsync<ComponentPreview>(new() { ["Component"] = "Nope" });
        Assert.Contains("ComponentPreview error", unknown);
        Assert.Contains("Unknown component &lt;Nope&gt;.", unknown);
    }

    [Fact]
    public void Links_SlugHashAndIssueUrls()
    {
        Assert.Equal("button-click-demo", PreviewLinks.Slug("ButtonClickDemo"));
        Assert.Equal("my-card", PreviewLinks.Slug("My Card!"));
        Assert.Equal("", PreviewLinks.Slug(null));

        var h = PreviewLinks.ShortHash("<Button />");
        Assert.Equal(6, h.Length);
        Assert.Equal(h, PreviewLinks.ShortHash("<Button />"));
        Assert.NotEqual(h, PreviewLinks.ShortHash("<Button>x</Button>"));

        Assert.Null(PreviewLinks.IssueTracker(new ShellDocsOptions()));
        Assert.Equal("https://github.com/a/b/issues/new", PreviewLinks.IssueTracker(new ShellDocsOptions { GitHubRepo = "/a/b/" }));
        Assert.Equal("https://x.dev/new", PreviewLinks.IssueTracker(new ShellDocsOptions { GitHubRepo = "a/b", IssueTrackerUrl = "https://x.dev/new" }));
        Assert.Equal("/docs/x#p", PreviewLinks.PageUrl(new ShellDocsOptions(), "/docs/x", "p"));
    }
}
