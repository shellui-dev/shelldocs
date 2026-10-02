using System.Text.RegularExpressions;
using ShellDocs.Components;
using ShellDocs.Components.Chrome;
using Xunit;

namespace ShellDocs.Tests;

// content/docs: introduction, components/index with form/index and form/button.
// docs/ is the top-level section, so components and form are collapsible.
public class SidebarIndexPageTests : IDisposable
{
    private readonly string _root;

    public SidebarIndexPageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-sidebar-index-" + Guid.NewGuid().ToString("N"));
        Write("docs/introduction.md", "Introduction");
        Write("docs/components/index.md", "Components");
        Write("docs/components/form/index.md", "Form Components");
        Write("docs/components/form/button.md", "Button");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void Write(string rel, string title)
    {
        var full = Path.Combine(_root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, $"---\ntitle: {title}\n---\n# {title}\n");
    }

    private Task<string> Render(string path) => new ComponentRenderHarness(o =>
    {
        o.ContentRoot = _root;
        o.LayoutVariant = DocsLayoutVariant.Sidebar;
    }, "http://localhost" + path).RenderAsync<DocsSidebar>();

    [Fact]
    public async Task IndexPage_IsTheSectionsLink_NotASecondItem()
    {
        var html = await Render("/docs/introduction");

        Assert.Single(Regex.Matches(html, "href=\"/docs/components\""));
        Assert.Matches("<a class=\"sidebar-section-link \" href=\"/docs/components\"", html);
        Assert.DoesNotMatch("class=\"sidebar-item[^\"]*\" href=\"/docs/components\"", html);
        Assert.Contains("aria-expanded=\"false\" aria-label=\"Toggle Components\"", html);
    }

    [Fact]
    public async Task CollapsibleSection_LinksItsIndex_AndKeepsAChevronToggle()
    {
        var html = await Render("/docs/components/form");

        Assert.Single(Regex.Matches(html, "href=\"/docs/components/form\""));
        Assert.Contains("class=\"sidebar-section-label sidebar-section-row\"", html);
        Assert.Matches("<a class=\"sidebar-section-link active\" href=\"/docs/components/form\"", html);
        Assert.Matches("<button type=\"button\" class=\"sidebar-section-toggle\" aria-expanded=\"true\" aria-label=\"Toggle [^\"]+\"", html);
        Assert.Contains("href=\"/docs/components/form/button\"", html);
        Assert.DoesNotContain(">Form Components<", html);
    }
}
