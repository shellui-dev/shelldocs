using ShellDocs.Core;
using Xunit;

namespace ShellDocs.Tests;

public class NavigationGraphBuilderTests : IDisposable
{
    private readonly string _root;

    public NavigationGraphBuilderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void WriteMd(string relativePath, string title, int order = 0)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, $"---\ntitle: {title}\norder: {order}\n---\n# {title}\n");
    }

    private void WriteMeta(string folder, string json)
    {
        var full = Path.Combine(_root, folder, "meta.json");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, json);
    }

    [Fact]
    public void Build_SingleFile_ResolvesByUrl()
    {
        WriteMd("intro.md", "Introduction");

        var graph = NavigationGraphBuilder.Build(_root);
        var node = graph.ResolveByUrl("/intro");

        Assert.NotNull(node);
        Assert.Equal("Introduction", node!.Title);
        Assert.Equal(NodeKind.Page, node.Kind);
    }

    [Fact]
    public void Build_UsesFrontmatterTitle_FallsBackToSlug()
    {
        WriteMd("with-title.md", "Explicit");
        var noFrontmatter = Path.Combine(_root, "no-title.md");
        File.WriteAllText(noFrontmatter, "# Just markdown");

        var graph = NavigationGraphBuilder.Build(_root);

        Assert.Equal("Explicit", graph.ResolveByUrl("/with-title")!.Title);
        Assert.Equal("No Title", graph.ResolveByUrl("/no-title")!.Title);
    }

    [Fact]
    public void Build_NestedFolders_AreSections()
    {
        WriteMd("docs/introduction.md", "Introduction");
        WriteMd("docs/installation.md", "Installation");
        WriteMd("components/button.md", "Button");

        var graph = NavigationGraphBuilder.Build(_root);

        Assert.NotNull(graph.ResolveByUrl("/docs/introduction"));
        Assert.NotNull(graph.ResolveByUrl("/docs/installation"));
        Assert.NotNull(graph.ResolveByUrl("/components/button"));

        var sections = graph.Root.Children.Where(c => c.Kind == NodeKind.Section).ToList();
        Assert.Contains(sections, s => s.Title == "Components");
        Assert.Contains(sections, s => s.Title == "Docs");
    }

    [Fact]
    public void Build_UsesMetaJsonOrdering()
    {
        WriteMd("alpha.md", "Alpha");
        WriteMd("bravo.md", "Bravo");
        WriteMd("charlie.md", "Charlie");
        WriteMeta("", """{ "pages": ["charlie", "alpha", "bravo"] }""");

        var graph = NavigationGraphBuilder.Build(_root);
        var titles = graph.Root.Children.Select(c => c.Title).ToList();

        Assert.Equal(new[] { "Charlie", "Alpha", "Bravo" }, titles);
    }

    [Fact]
    public void Build_MetaJsonDivider_ProducesDividerNode()
    {
        WriteMd("a.md", "A");
        WriteMd("b.md", "B");
        WriteMeta("", """{ "pages": ["a", "---", "b"] }""");

        var graph = NavigationGraphBuilder.Build(_root);

        var kinds = graph.Root.Children.Select(c => c.Kind).ToList();
        Assert.Equal(new[] { NodeKind.Page, NodeKind.Divider, NodeKind.Page }, kinds);
    }

    [Fact]
    public void Build_MetaJsonSubsection_CreatesNamedSection()
    {
        WriteMd("table.md", "Table");
        WriteMd("card.md", "Card");
        WriteMeta("", """
        {
          "pages": [
            {
              "title": "Data Display",
              "pages": ["table", "card"]
            }
          ]
        }
        """);

        var graph = NavigationGraphBuilder.Build(_root);
        var section = Assert.Single(graph.Root.Children);

        Assert.Equal("Data Display", section.Title);
        Assert.Equal(NodeKind.Section, section.Kind);
        Assert.Equal(new[] { "Table", "Card" }, section.Children.Select(c => c.Title));
    }

    [Fact]
    public void Build_UnknownSlugInMetaJson_IsSilentlySkipped()
    {
        WriteMd("real.md", "Real");
        WriteMeta("", """{ "pages": ["real", "does-not-exist"] }""");

        var graph = NavigationGraphBuilder.Build(_root);
        var titles = graph.Root.Children.Select(c => c.Title).ToList();

        Assert.Equal(new[] { "Real" }, titles);
    }

    [Fact]
    public void Build_HiddenSlug_IsExcludedFromSidebar_ButUrlStillResolves()
    {
        WriteMd("visible.md", "Visible");
        WriteMd("secret.md", "Secret");
        WriteMeta("", """{ "pages": ["visible"], "hidden": ["secret"] }""");

        var graph = NavigationGraphBuilder.Build(_root);
        var titles = graph.Root.Children.Select(c => c.Title).ToList();

        // Sidebar shows only visible (secret excluded despite being on disk).
        Assert.Equal(new[] { "Visible" }, titles);

        // But secret's URL still resolves (the whole point of `hidden` vs
        // deleting the file: the dropdown/direct link still works).
        var secretNode = graph.ResolveByUrl("/secret");
        Assert.NotNull(secretNode);
        Assert.Equal("Secret", secretNode!.Title);
    }

    [Fact]
    public void Build_HiddenFolder_IsExcludedFromSidebar_ButChildUrlsResolve()
    {
        WriteMd("visible.md", "Visible");
        WriteMd("packages/components.md", "Components");
        WriteMd("packages/cli.md", "CLI");
        WriteMeta("", """{ "pages": ["visible"], "hidden": ["packages"] }""");

        var graph = NavigationGraphBuilder.Build(_root);
        var titles = graph.Root.Children.Select(c => c.Title).ToList();

        // Sidebar has no "Packages" section.
        Assert.Equal(new[] { "Visible" }, titles);

        // But child URLs still route via ResolveByUrl.
        Assert.NotNull(graph.ResolveByUrl("/packages/components"));
        Assert.NotNull(graph.ResolveByUrl("/packages/cli"));
    }

    [Fact]
    public void Build_HiddenTakesPrecedenceOverPages()
    {
        // A slug listed in BOTH hidden and pages: hidden wins.
        WriteMd("alpha.md", "Alpha");
        WriteMd("beta.md", "Beta");
        WriteMeta("", """{ "pages": ["alpha", "beta"], "hidden": ["beta"] }""");

        var graph = NavigationGraphBuilder.Build(_root);
        var titles = graph.Root.Children.Select(c => c.Title).ToList();

        Assert.Equal(new[] { "Alpha" }, titles);
        Assert.NotNull(graph.ResolveByUrl("/beta"));
    }

    [Fact]
    public void Build_HiddenSlug_IsAlsoExcludedFromAutoAppend()
    {
        // No `pages` array. Without hidden support, auto-append would surface
        // secret alongside visible. With hidden, secret still hidden.
        WriteMd("visible.md", "Visible");
        WriteMd("secret.md", "Secret");
        WriteMeta("", """{ "hidden": ["secret"] }""");

        var graph = NavigationGraphBuilder.Build(_root);
        var titles = graph.Root.Children.Select(c => c.Title).ToList();

        Assert.Equal(new[] { "Visible" }, titles);
    }

    [Fact]
    public void Build_MdFileNotInMetaJson_IsAppendedAfterExplicitOrdering()
    {
        // Meta lists only `alpha`, but `bravo.md` exists on disk.
        // Bravo should surface at the end, not silently disappear (the
        // `shelldocs add` DX gap fix).
        WriteMd("alpha.md", "Alpha");
        WriteMd("bravo.md", "Bravo");
        WriteMeta("", """{ "pages": ["alpha"] }""");

        var graph = NavigationGraphBuilder.Build(_root);
        var titles = graph.Root.Children.Select(c => c.Title).ToList();

        Assert.Equal(new[] { "Alpha", "Bravo" }, titles);
    }

    [Fact]
    public void Build_SubfolderNotInMetaJson_AppearsAsSectionAfterExplicitOrdering()
    {
        WriteMd("intro.md", "Intro");
        WriteMd("components/button.md", "Button");
        WriteMeta("", """{ "pages": ["intro"] }""");

        var graph = NavigationGraphBuilder.Build(_root);
        var titles = graph.Root.Children.Select(c => c.Title).ToList();

        Assert.Equal(new[] { "Intro", "Components" }, titles);
    }

    [Fact]
    public void Build_UnreferencedItems_AreAlphabetical()
    {
        WriteMd("first.md", "First");   // in meta
        WriteMd("charlie.md", "Charlie"); // not in meta
        WriteMd("alpha.md", "Alpha");     // not in meta
        WriteMd("bravo.md", "Bravo");     // not in meta
        WriteMeta("", """{ "pages": ["first"] }""");

        var graph = NavigationGraphBuilder.Build(_root);
        var titles = graph.Root.Children.Select(c => c.Title).ToList();

        Assert.Equal(new[] { "First", "Alpha", "Bravo", "Charlie" }, titles);
    }

    [Fact]
    public void Build_ExplicitOrderingIsPreserved_ForItemsInMetaJson()
    {
        // Verify the auto-append doesn't break the existing "meta.json controls
        // ordering for explicitly-listed items" contract.
        WriteMd("alpha.md", "Alpha");
        WriteMd("bravo.md", "Bravo");
        WriteMd("charlie.md", "Charlie");
        WriteMeta("", """{ "pages": ["charlie", "alpha"] }""");

        var graph = NavigationGraphBuilder.Build(_root);
        var titles = graph.Root.Children.Select(c => c.Title).ToList();

        // Charlie + Alpha in the meta-specified order, THEN Bravo appended.
        Assert.Equal(new[] { "Charlie", "Alpha", "Bravo" }, titles);
    }

    [Fact]
    public void Build_ThrowsOnMissingContentRoot()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => NavigationGraphBuilder.Build(Path.Combine(_root, "does-not-exist")));
    }

    [Fact]
    public void GetPrevNext_ReturnsAdjacentPages()
    {
        WriteMd("first.md", "First", order: 1);
        WriteMd("second.md", "Second", order: 2);
        WriteMd("third.md", "Third", order: 3);

        var graph = NavigationGraphBuilder.Build(_root);
        var second = graph.ResolveByUrl("/second")!;

        var (prev, next) = graph.GetPrevNext(second);

        Assert.Equal("First", prev?.Title);
        Assert.Equal("Third", next?.Title);
    }

    [Fact]
    public void GetPrevNext_HandlesBoundaries()
    {
        WriteMd("only.md", "Only");

        var graph = NavigationGraphBuilder.Build(_root);
        var only = graph.ResolveByUrl("/only")!;

        var (prev, next) = graph.GetPrevNext(only);

        Assert.Null(prev);
        Assert.Null(next);
    }

    [Fact]
    public void GetBreadcrumb_WalksUpFromLeafToTopLevel()
    {
        WriteMd("docs/theming/tokens.md", "Tokens");

        var graph = NavigationGraphBuilder.Build(_root);
        var leaf = graph.ResolveByUrl("/docs/theming/tokens")!;
        var trail = graph.GetBreadcrumb(leaf);

        Assert.Equal(new[] { "Docs", "Theming", "Tokens" }, trail.Select(n => n.Title));
    }

    [Fact]
    public void ResolveByUrl_IsCaseInsensitiveAndTrailingSlashTolerant()
    {
        WriteMd("Button.md", "Button");

        var graph = NavigationGraphBuilder.Build(_root);

        Assert.NotNull(graph.ResolveByUrl("/button"));
        Assert.NotNull(graph.ResolveByUrl("/BUTTON"));
        Assert.NotNull(graph.ResolveByUrl("/button/"));
        Assert.NotNull(graph.ResolveByUrl("button"));
    }
}
