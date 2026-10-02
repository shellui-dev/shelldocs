using Microsoft.AspNetCore.Components;
using ShellDocs.Components;
using ShellDocs.Core;
using Xunit;

namespace ShellDocs.Tests;

public class UrlPathTests
{
    [Theory]
    [InlineData("docs/v0.2.1/", "/docs/v0.2.1")]
    [InlineData("/docs/v0.3/intro?x=1#h", "/docs/v0.3/intro")]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("//", "/")]
    public void Normalize(string input, string expected) => Assert.Equal(expected, UrlPath.Normalize(input));

    [Theory]
    [InlineData("/docs/v0.3/intro", "/docs/v0.3", true)]
    [InlineData("/docs/v0.3", "/docs/v0.3", true)]
    [InlineData("/docs/v0.3/", "/docs/v0.3", true)]
    [InlineData("/DOCS/V0.3/intro", "/docs/v0.3", true)]
    [InlineData("/docs/v0.30/intro", "/docs/v0.3", false)]
    [InlineData("/docs/v0.3-beta", "/docs/v0.3", false)]
    [InlineData("/docs/v0.2.1/intro", "/docs/v0.2", false)]
    [InlineData("/anything", "/", true)]
    public void IsUnder_IsSegmentAware(string path, string root, bool expected)
        => Assert.Equal(expected, UrlPath.IsUnder(path, root));

    [Fact]
    public void RelativeTo_And_Combine_RoundTrip()
    {
        Assert.Equal("/components/form/button", UrlPath.RelativeTo("/docs/v0.3/components/form/button", "/docs/v0.3"));
        Assert.Equal("", UrlPath.RelativeTo("/docs/v0.3", "/docs/v0.3"));
        Assert.Null(UrlPath.RelativeTo("/docs/v0.2.1/x", "/docs/v0.3"));
        Assert.Equal("/docs/v0.2.1/components/form/button", UrlPath.Combine("/docs/v0.2.1", "/components/form/button"));
        Assert.Equal("/docs/v0.2.1", UrlPath.Combine("/docs/v0.2.1/", ""));
    }
}

// Mirrors the ShellUI docs layout (content/docs/{v0.3,v0.2.1}). Without meta.json
// the flat page order is v0.2.1/*, v0.3/*, then docs/introduction.
public class VersioningTests : IDisposable
{
    private readonly string _root;
    private readonly NavigationGraph _graph;

    public VersioningTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-ver-" + Guid.NewGuid().ToString("N"));
        Write("docs/introduction.md", "Unversioned intro");
        foreach (var v in new[] { "v0.3", "v0.2.1" })
        {
            Write($"docs/{v}/introduction.md", $"Intro {v}");
            Write($"docs/{v}/installation/cli.md", $"Install CLI {v}");
            Write($"docs/{v}/components/form/button.md", $"Button {v}");
            Write($"docs/{v}/cli/build.md", $"Build {v}");
        }
        Write("docs/v0.3/cli/dev.md", "Dev v0.3"); // only exists in v0.3
        Write("docs/v0.3/whats-new.md", "What's new");  // only exists in v0.3
        Write("docs/v0.3/installation/docker.md", "Docker"); // only exists in v0.3
        _graph = NavigationGraphBuilder.Build(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void Write(string rel, string title)
    {
        var full = Path.Combine(_root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, $"---\ntitle: {title}\n---\n# {title}\n\n## Usage\n\nBody for {title}.\n");
    }

    private static ShellDocsOptions Options(Action<ShellDocsOptions>? extra = null)
    {
        var o = new ShellDocsOptions();
        o.AddVersion("v0.3", "v0.3.0", "/docs/v0.3", "Current stable", latest: true);
        o.AddVersion("v0.2.1", "v0.2.1", "/docs/v0.2.1", "Previous release");
        extra?.Invoke(o);
        return o;
    }

    private DocsVersionResolver Resolver(Action<ShellDocsOptions>? extra = null) => new(Options(extra), _graph);

    [Fact]
    public void DottedVersionFolders_KeepUrlsIntact()
    {
        var node = _graph.ResolveByUrl("/docs/v0.2.1/components/form/button");
        Assert.NotNull(node);
        Assert.Equal("Button v0.2.1", node!.Title);
        Assert.Equal("/docs/v0.2.1/components/form/button", node.Url);
        Assert.NotNull(_graph.ResolveByUrl("/docs/v0.2.1/introduction/"));
        Assert.Contains("/docs/v0.2.1/cli/build", _graph.AllUrls);
    }

    [Fact]
    public void FindFolder_ReturnsVersionSection()
    {
        var folder = _graph.FindFolder("/docs/v0.2.1");
        Assert.NotNull(folder);
        Assert.Equal(NodeKind.Section, folder!.Kind);
        Assert.Contains(folder.Children, c => c.Url == "/docs/v0.2.1/introduction");
        Assert.Null(_graph.FindFolder("/docs/v9"));
    }

    [Fact]
    public void FirstPageUnder_IsSegmentAware()
    {
        Assert.StartsWith("/docs/v0.3/", _graph.FirstPageUnder("/docs/v0.3")!.Url);
        Assert.Null(_graph.FirstPageUnder("/docs/v0"));
    }

    [Fact]
    public void ScopedPrevNext_SkipsOutOfScopePages()
    {
        var first = _graph.FirstPageUnder("/docs/v0.3")!;
        var (prev, next) = _graph.GetPrevNext(first, n => UrlPath.IsUnder(n.Url, "/docs/v0.3"));
        Assert.Null(prev);
        Assert.NotNull(next);
        Assert.True(UrlPath.IsUnder(next!.Url, "/docs/v0.3"));

        // Unscoped, the same page's prev crosses into v0.2.1.
        var (unscopedPrev, _) = _graph.GetPrevNext(first);
        Assert.True(UrlPath.IsUnder(unscopedPrev!.Url, "/docs/v0.2.1"));
    }

    [Fact]
    public void AddVersion_StoresRecord()
    {
        var o = Options();
        Assert.Equal(2, o.Versions.Count);
        Assert.Equal(new DocsVersion("v0.3", "v0.3.0", "/docs/v0.3", "Current stable", true), o.Versions[0]);
        Assert.False(o.Versions[1].IsLatest);
        Assert.Throws<ArgumentException>(() => new ShellDocsOptions().AddVersion("", "x", "/x"));
        Assert.Throws<ArgumentException>(() => new ShellDocsOptions().AddVersion("x", "x", " "));
    }

    [Fact]
    public void Current_PrefersContainingVersion_ThenLatest_ThenFirst()
    {
        var r = Resolver();
        Assert.Equal("v0.2.1", r.Current("/docs/v0.2.1/introduction")!.Id);
        Assert.Equal("v0.3", r.Current("/docs/v0.3")!.Id);
        Assert.Equal("v0.3", r.Current("/docs/introduction")!.Id); // outside → latest
        Assert.Null(r.FindContaining("/docs/introduction"));

        var noLatest = new ShellDocsOptions();
        noLatest.AddVersion("b", "B", "/docs/b");
        noLatest.AddVersion("a", "A", "/docs/a");
        Assert.Equal("b", new DocsVersionResolver(noLatest, _graph).Current("/elsewhere")!.Id);

        Assert.Null(new DocsVersionResolver(new ShellDocsOptions(), _graph).Current("/docs/v0.3"));
    }

    [Fact]
    public void Current_LongestRootWins_ForNestedRoots()
    {
        var o = new ShellDocsOptions();
        o.AddVersion("all", "All", "/docs");
        o.AddVersion("v0.3", "v0.3", "/docs/v0.3");
        Assert.Equal("v0.3", new DocsVersionResolver(o, _graph).Current("/docs/v0.3/introduction")!.Id);
    }

    [Fact]
    public void SidebarNodes_InsideVersion_AreThatFoldersChildren()
    {
        var r = Resolver();
        var nodes = r.GetSidebarNodes("/docs/v0.2.1/components/form/button");
        var urls = Flatten(nodes).Select(n => n.Url).Where(u => u != "").ToList();
        Assert.NotEmpty(urls);
        Assert.All(urls, u => Assert.StartsWith("/docs/v0.2.1/", u));
        Assert.Contains(nodes, n => n.Kind == NodeKind.Section && n.Title == "Components");
    }

    [Fact]
    public void SidebarNodes_OutsideVersions_HideVersionFolders()
    {
        var r = Resolver();
        var docs = Assert.Single(r.GetSidebarNodes("/docs/introduction"));
        Assert.Equal("Docs", docs.Title);
        Assert.False(r.IsHiddenInSidebar(docs));
        Assert.True(r.IsHiddenInSidebar(_graph.FindFolder("/docs/v0.3")!));
        Assert.True(r.IsHiddenInSidebar(_graph.FindFolder("/docs/v0.2.1")!));
        Assert.Equal(new[] { "/docs/introduction" },
            docs.Children.Where(c => !r.IsHiddenInSidebar(c)).Select(c => c.Url));
    }

    [Fact]
    public void SidebarNodes_SectionHoldingOnlyVersions_IsHidden()
    {
        var o = new ShellDocsOptions();
        o.AddVersion("v0.3", "v0.3", "/docs/v0.3");
        var r = new DocsVersionResolver(o, _graph);
        var docs = _graph.Root.Children.Single();
        // docs/ still has introduction.md, so it stays; a folder of only versions would not.
        Assert.False(r.IsHiddenInSidebar(docs));
        Assert.True(r.IsHiddenInSidebar(_graph.FindFolder("/docs/v0.3")!));
        Assert.False(r.IsHiddenInSidebar(_graph.FindFolder("/docs/v0.2.1")!)); // not a configured version
    }

    [Fact]
    public void SidebarNodes_WithoutVersions_AreWholeTree()
        => Assert.Same(_graph.Root.Children, new DocsVersionResolver(new ShellDocsOptions(), _graph).GetSidebarNodes("/docs/v0.3/introduction"));

    private static IEnumerable<NavigationNode> Flatten(IEnumerable<NavigationNode> nodes)
        => nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));

    [Fact]
    public void VersionHref_KeepsSameRelativePage()
    {
        var r = Resolver();
        var target = r.Versions.Single(v => v.Id == "v0.2.1");
        Assert.Equal("/docs/v0.2.1/components/form/button", r.GetVersionHref("/docs/v0.3/components/form/button", target));
    }

    [Fact]
    public void VersionHref_MissingPage_FallsBackToFirstPageOfTargetVersion()
    {
        var r = Resolver();
        var target = r.Versions.Single(v => v.Id == "v0.2.1");
        var href = r.GetVersionHref("/docs/v0.3/whats-new", target);
        Assert.Equal(_graph.FirstPageUnder("/docs/v0.2.1")!.Url, href);
    }

    [Fact]
    public void VersionHref_MissingPage_KeepsCurrentPackage()
    {
        var r = Resolver(o =>
        {
            o.AddPackage("core", "Core", "", "/docs/{version}/introduction");
            o.AddPackage("install", "Install", "", "/docs/{version}/installation");
        });
        var target = r.Versions.Single(v => v.Id == "v0.2.1");
        // docker.md exists only in v0.3; expect the Install package, not the version's first page.
        Assert.Equal("/docs/v0.2.1/cli/build", _graph.FirstPageUnder("/docs/v0.2.1")!.Url);
        Assert.Equal("/docs/v0.2.1/installation/cli", r.GetVersionHref("/docs/v0.3/installation/docker", target));
    }

    [Fact]
    public void VersionHref_FromUnversionedPage_GoesToFirstPage()
    {
        var r = Resolver();
        var target = r.Versions.Single(v => v.Id == "v0.2.1");
        Assert.StartsWith("/docs/v0.2.1/", r.GetVersionHref("/docs/introduction", target));
    }

    [Fact]
    public void PackageRoot_VersionToken_ResolvesToCurrentVersion()
    {
        var r = Resolver(o => o.AddPackage("cli", "CLI", "", "/docs/{version}/cli"));
        var pkg = Options(o => o.AddPackage("cli", "CLI", "", "/docs/{version}/cli")).Packages[0];
        Assert.True(pkg.IsVersioned);
        Assert.Equal("/docs/v0.2.1/cli", r.ResolvePackageRoot(pkg, "/docs/v0.2.1/introduction"));
        Assert.Equal("/docs/v0.3/cli", r.ResolvePackageRoot(pkg, "/docs/introduction"));
    }

    [Fact]
    public void PackageHref_VersionedRootWithoutIndex_LandsOnFirstPage()
    {
        var r = Resolver(o => o.AddPackage("cli", "CLI", "", "/docs/{version}/cli"));
        var pkg = r.SelectPackage("/docs/v0.2.1/cli/build")!;
        Assert.Equal("/docs/v0.2.1/cli/build", r.GetPackageHref(pkg, "/docs/v0.2.1/introduction"));
    }

    [Fact]
    public void PackageHref_Unversioned_IsVerbatim()
    {
        var r = Resolver(o => o.AddPackage("home", "Home", "", "/custom-page"));
        Assert.Equal("/custom-page", r.GetPackageHref(r.SelectPackage("/")!, "/docs/v0.3/introduction"));
    }

    [Fact]
    public void SelectPackage_UsesResolvedLongestSegmentPrefix()
    {
        var r = Resolver(o =>
        {
            o.AddPackage("docs", "Docs", "", "/docs/{version}");
            o.AddPackage("cli", "CLI", "", "/docs/{version}/cli");
            o.AddPackage("clientlib", "Client", "", "/docs/{version}/client");
        });
        Assert.Equal("cli", r.SelectPackage("/docs/v0.2.1/cli/build")!.Id);
        Assert.Equal("docs", r.SelectPackage("/docs/v0.2.1/introduction")!.Id);
        Assert.Equal("docs", r.SelectPackage("/docs/v0.3/clix")!.Id);  // not segment-prefixed by /cli
        Assert.Equal("docs", r.SelectPackage("/elsewhere")!.Id);        // falls back to Packages[0]
    }

    [Fact]
    public void FilterSearch_CurrentVersionPlusUnversioned()
    {
        var r = Resolver();
        var index = SearchIndex.FromGraph(_graph);
        var urls = r.FilterSearch(index.Entries, "/docs/v0.2.1/introduction").Select(e => e.Url).ToList();

        Assert.Contains("/docs/v0.2.1/introduction", urls);
        Assert.Contains("/docs/v0.2.1/introduction#usage", urls);
        Assert.Contains("/docs/introduction", urls);
        Assert.DoesNotContain(urls, u => u.StartsWith("/docs/v0.3/"));
    }

    [Fact]
    public void FilterSearch_OutsideVersions_UsesLatest()
    {
        var urls = Resolver().FilterSearch(SearchIndex.FromGraph(_graph).Entries, "/docs/introduction").Select(e => e.Url).ToList();
        Assert.Contains(urls, u => u.StartsWith("/docs/v0.3/"));
        Assert.DoesNotContain(urls, u => u.StartsWith("/docs/v0.2.1/"));
    }

    [Fact]
    public void FilterSearch_NoVersions_IsUnfiltered()
    {
        var entries = SearchIndex.FromGraph(_graph).Entries;
        Assert.Equal(entries.Count, new DocsVersionResolver(new ShellDocsOptions(), _graph).FilterSearch(entries, "/").Count());
    }

    [Fact]
    public void PageState_PrevNext_NeverCrossVersionBoundary()
    {
        var r = Resolver();
        var first = _graph.FirstPageUnder("/docs/v0.3")!;
        var state = new DocsPageState(_graph, new StubNav("http://localhost" + first.Url), r);
        state.SetDocument(null);
        Assert.Null(state.Prev);
        Assert.True(UrlPath.IsUnder(state.Next!.Url, "/docs/v0.3"));

        // Last v0.3 page: unscoped next would be the unversioned /docs/introduction.
        var last = _graph.Flatten().Last(n => n.Kind == NodeKind.Page && UrlPath.IsUnder(n.Url, "/docs/v0.3"));
        var lastState = new DocsPageState(_graph, new StubNav("http://localhost" + last.Url), r);
        lastState.SetDocument(null);
        Assert.Null(lastState.Next);
        Assert.True(UrlPath.IsUnder(lastState.Prev!.Url, "/docs/v0.3"));
    }

    [Fact]
    public void PageState_Breadcrumb_OmitsVersionFolder()
    {
        var state = new DocsPageState(_graph, new StubNav("http://localhost/docs/v0.2.1/components/form/button"), Resolver());
        state.SetDocument(null);
        var titles = state.Breadcrumbs.Select(b => b.Title).ToList();
        Assert.Equal(new[] { "Docs", "Components", "Form", "Button v0.2.1" }, titles);
    }

    [Fact]
    public void PageState_WithoutResolver_KeepsLegacyBehaviour()
    {
        var state = new DocsPageState(_graph, new StubNav("http://localhost/docs/v0.2.1/components/form/button"));
        state.SetDocument(null);
        Assert.Contains(state.Breadcrumbs, b => b.Title == "V0.2.1");
    }

    internal sealed class StubNav : NavigationManager
    {
        public StubNav(string uri) => Initialize("http://localhost/", uri);
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
