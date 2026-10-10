using System.Text.RegularExpressions;
using ShellDocs.Components;
using ShellDocs.Components.Chrome;
using ShellDocs.Components.Layouts;
using Xunit;

namespace ShellDocs.Tests;

public class SidebarCollapseTests : IDisposable
{
    private readonly string _root;

    public SidebarCollapseTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-collapse-" + Guid.NewGuid().ToString("N"));
        var page = Path.Combine(_root, "docs", "getting-started.md");
        Directory.CreateDirectory(Path.GetDirectoryName(page)!);
        File.WriteAllText(page, "---\ntitle: Getting started\n---\n# Getting started\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private ComponentRenderHarness Harness(DocsLayoutVariant layout = DocsLayoutVariant.Sidebar) => new(o =>
    {
        o.ContentRoot = _root;
        o.LayoutVariant = layout;
    }, "http://localhost/docs/getting-started");

    [Fact]
    public async Task SidebarCollapse_IsStaticHostFriendly()
    {
        var layout = await Harness().RenderAsync<DocsLayout>();
        Assert.Contains("data-sidebar-collapsed=\"false\"", layout);
        Assert.Matches("class=\"docs-collapsed-tool\"[^>]*data-sidebar-collapse-toggle[^>]*aria-label=\"Expand sidebar\"", layout);
        Assert.DoesNotContain("docs-sidebar-collapsed", layout);

        var header = await Harness().RenderAsync<DocsSidebarHeader>();
        var toggle = Regex.Match(header, "<button[^>]*docs-sidebar-collapse[^>]*>").Value;
        Assert.Contains("data-sidebar-collapse-toggle", toggle);
        Assert.DoesNotContain("blazor:onclick", toggle);

        var topNav = await Harness(DocsLayoutVariant.TopNav).RenderAsync<DocsLayout>();
        Assert.DoesNotContain("docs-collapsed-toolbar", topNav);
    }

    [Fact]
    public void ShelldocsJs_TogglesAndRemembersTheCollapse()
    {
        var js = File.ReadAllText(FindAsset("shelldocs.js"));
        Assert.Contains("[data-sidebar-collapse-toggle]", js);
        Assert.Contains("'shelldocs-sidebar'", js);
        Assert.Contains("restoreCollapse(added[n])", js);
    }

    private static string FindAsset(string name)
    {
        var testDir = Path.GetDirectoryName(typeof(SidebarCollapseTests).Assembly.Location)!;
        foreach (var candidate in new[]
        {
            Path.Combine(testDir, "wwwroot", "_content", "ShellDocs.Components", name),
            Path.Combine(testDir, "..", "..", "..", "..", "..", "src", "ShellDocs.Components", "wwwroot", name)
        })
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full)) return full;
        }
        throw new FileNotFoundException(name);
    }
}
