using ShellDocs.Components;
using ShellDocs.Components.Chrome;
using Xunit;

namespace ShellDocs.Tests;

public class VersionChromeRenderTests : IDisposable
{
    private readonly string _root;

    public VersionChromeRenderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-chrome-" + Guid.NewGuid().ToString("N"));
        foreach (var v in new[] { "v0.3", "v0.2.1" })
        {
            Write($"docs/{v}/introduction.md", $"Intro {v}");
            Write($"docs/{v}/components/form/button.md", $"Button {v}");
            Write($"docs/{v}/cli/build.md", $"Build {v}");
        }
        Write("docs/v0.3/cli/dev.md", "Dev v0.3");
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

    private ComponentRenderHarness Harness(string path, Action<ShellDocsOptions>? extra = null, bool versions = true) => new(o =>
    {
        o.ContentRoot = _root;
        if (versions)
        {
            o.AddVersion("v0.3", "v0.3.0", "/docs/v0.3", "Current stable", latest: true);
            o.AddVersion("v0.2.1", "v0.2.1", "/docs/v0.2.1", "Previous release");
        }
        extra?.Invoke(o);
    }, "http://localhost" + path);

    [Fact]
    public async Task VersionSelector_ServerRendersCurrentLabel_BadgeCheck_AndSamePageHrefs()
    {
        var html = await Harness("/docs/v0.3/components/form/button").RenderAsync<VersionSelector>();

        Assert.Contains("class=\"ver ver-sidebar\" data-open=\"false\"", html);
        Assert.Contains("<span class=\"ver-value\"", html);
        Assert.Contains("v0.3.0</span>", html);
        Assert.Contains("href=\"/docs/v0.2.1/components/form/button\"", html);
        Assert.Contains("href=\"/docs/v0.3/components/form/button\"", html);
        Assert.Contains("ver-badge", html);
        Assert.Contains("ver-option-check", html);
        Assert.Contains("aria-selected=\"true\"", html);
        Assert.DoesNotContain("blazor:onclick", html);
    }

    [Fact]
    public async Task VersionSelector_MissingPage_FallsBackToTargetVersion()
    {
        var html = await Harness("/docs/v0.3/cli/dev").RenderAsync<VersionSelector>();
        Assert.Contains("v0.3.0</span>", html);
        Assert.DoesNotContain("/docs/v0.2.1/cli/dev", html);
        Assert.Contains("href=\"/docs/v0.2.1/", html);
    }

    [Fact]
    public async Task VersionSelector_HiddenWithFewerThanTwoVersions()
    {
        var html = await Harness("/docs/v0.3/introduction", o => o.AddVersion("v0.3", "v0.3.0", "/docs/v0.3"), versions: false)
            .RenderAsync<VersionSelector>();
        Assert.True(string.IsNullOrWhiteSpace(html), html);
    }

    [Fact]
    public async Task PackageSelector_ResolvesVersionToken_AndKeepsVersion()
    {
        var html = await Harness("/docs/v0.2.1/cli/build", o =>
        {
            o.AddPackage("core", "Core", "Core docs", "/docs/{version}/introduction");
            o.AddPackage("cli", "CLI", "Command line", "/docs/{version}/cli");
        }).RenderAsync<PackageSelector>();

        Assert.Contains("href=\"/docs/v0.2.1/introduction\"", html);
        Assert.Contains("href=\"/docs/v0.2.1/cli/build\"", html); // root has no index → first page
        Assert.Contains(">CLI</span>", html.Substring(0, html.IndexOf("pkg-menu", StringComparison.Ordinal))); // selected in trigger
        Assert.DoesNotContain("{version}", html);
    }

    [Fact]
    public async Task Sidebar_IsScopedToCurrentVersion_AndCarriesVersionSelector()
    {
        var html = await Harness("/docs/v0.2.1/components/form/button", o => o.LayoutVariant = DocsLayoutVariant.Sidebar)
            .RenderAsync<DocsSidebar>();

        Assert.Contains("href=\"/docs/v0.2.1/introduction\"", html);
        Assert.Contains("href=\"/docs/v0.2.1/components/form/button\"", html);
        Assert.DoesNotContain("href=\"/docs/v0.3/introduction\"", html.Substring(html.IndexOf("sidebar-tree", StringComparison.Ordinal)));
        Assert.DoesNotContain("V0.2.1", html); // the version folder itself isn't a section
        Assert.Contains("class=\"ver ver-sidebar\"", html);
        // Active page's ancestor sections are server-rendered open.
        Assert.Contains("class=\"sidebar-item active\" href=\"/docs/v0.2.1/components/form/button\"", html);
    }

    [Fact]
    public async Task TopNav_HeaderCarriesDesktopSelector_SidebarCarriesDrawerCopy()
    {
        var header = await Harness("/docs/v0.3/introduction", o => o.LayoutVariant = DocsLayoutVariant.TopNav).RenderAsync<DocsHeader>();
        Assert.Contains("class=\"ver ver-header\"", header);

        var sidebar = await Harness("/docs/v0.3/introduction", o => o.LayoutVariant = DocsLayoutVariant.TopNav).RenderAsync<DocsSidebar>();
        Assert.Contains("class=\"ver ver-drawer\"", sidebar);

        var home = await Harness("/", o => o.LayoutVariant = DocsLayoutVariant.TopNav)
            .RenderAsync<DocsHeader>(new() { ["ShowSearch"] = false, ["ShowVersionSelector"] = false });
        Assert.DoesNotContain("ver-header", home);
    }
}
