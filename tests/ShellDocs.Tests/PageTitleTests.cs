using ShellDocs.Components.Content;
using Xunit;

namespace ShellDocs.Tests;

public class PageTitleTests
{
    private const string WithFrontmatter = "---\ntitle: Button\ndescription: Clickable things.\n---\n\nBody text.\n";

    private static Task<string> Render(string markdown, bool renderTitle) => new ComponentRenderHarness(o =>
    {
        o.ContentRoot = Path.Combine(Path.GetTempPath(), "shelldocs-no-content-" + Guid.NewGuid().ToString("N"));
        o.RenderPageTitle = renderTitle;
    }).RenderAsync<MarkdownContent>(new() { ["Markdown"] = markdown });

    [Fact]
    public async Task On_RendersTitleAndDescription_AsTheHeader()
    {
        var html = await Render(WithFrontmatter, renderTitle: true);

        // [^>]* allows for the scoped-CSS attribute.
        Assert.Matches("<header class=\"docs-page-header\"[^>]*>\\s*<h1[^>]*>Button</h1>", html);
        Assert.Matches("<p class=\"docs-page-description\"[^>]*>Clickable things.</p>", html);
    }

    [Fact]
    public async Task On_ButTheBodyHasItsOwnH1_KeepsTheBodys()
    {
        var html = await Render("---\ntitle: Button\n---\n\n# Buttons everywhere\n\nBody.\n", renderTitle: true);

        Assert.DoesNotContain("docs-page-header", html);
        Assert.Contains("Buttons everywhere", html);
    }

    [Fact]
    public async Task Off_ByDefault()
    {
        var html = await Render(WithFrontmatter, renderTitle: false);
        Assert.DoesNotContain("docs-page-header", html);
    }
}
