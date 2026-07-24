using ShellDocs.Core;
using Xunit;

namespace ShellDocs.Tests;

public class SearchIndexTests : IDisposable
{
    private readonly string _root;

    public SearchIndexTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void WritePage(string relativePath, string title, string? description = null, string bodyHeadings = "")
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var desc = description is null ? "" : $"description: {description}\n";
        File.WriteAllText(full,
            $"---\ntitle: {title}\n{desc}---\n# {title}\n{bodyHeadings}\n");
    }

    [Fact]
    public void FromGraph_EmitsOneEntryPerPage()
    {
        WritePage("intro.md", "Introduction");
        var graph = NavigationGraphBuilder.Build(_root);
        var index = SearchIndex.FromGraph(graph);
        Assert.Single(index.Entries, e => e.Kind == SearchEntryKind.Page);
    }

    [Fact]
    public void FromGraph_EmitsHeadingEntriesForH2AndH3()
    {
        WritePage("intro.md", "Introduction", bodyHeadings: "## Setup\n\n### Install\n");
        var graph = NavigationGraphBuilder.Build(_root);
        var index = SearchIndex.FromGraph(graph);
        Assert.Contains(index.Entries, e => e.Kind == SearchEntryKind.Heading && e.Title == "Setup");
        Assert.Contains(index.Entries, e => e.Kind == SearchEntryKind.Heading && e.Title == "Install");
    }

    [Fact]
    public void FromGraph_HeadingEntriesUseAnchoredUrl()
    {
        WritePage("intro.md", "Introduction", bodyHeadings: "## Setup\n");
        var graph = NavigationGraphBuilder.Build(_root);
        var index = SearchIndex.FromGraph(graph);
        var setup = index.Entries.First(e => e.Title == "Setup");
        Assert.EndsWith("#setup", setup.Url);
    }

    [Fact]
    public void FromGraph_HeadingEntriesCarrySectionAsPageTitle()
    {
        WritePage("intro.md", "Introduction", bodyHeadings: "## Setup\n");
        var graph = NavigationGraphBuilder.Build(_root);
        var index = SearchIndex.FromGraph(graph);
        var setup = index.Entries.First(e => e.Title == "Setup");
        Assert.Equal("Introduction", setup.Section);
    }

    [Fact]
    public void FromGraph_PageEntryCarriesDescription()
    {
        WritePage("intro.md", "Introduction", description: "Get started with ShellDocs");
        var graph = NavigationGraphBuilder.Build(_root);
        var index = SearchIndex.FromGraph(graph);
        var page = index.Entries.First(e => e.Kind == SearchEntryKind.Page);
        Assert.Equal("Get started with ShellDocs", page.Description);
    }

    [Fact]
    public void FromGraph_PageEntryCarriesExtractedBody()
    {
        WritePage("intro.md", "Introduction",
            bodyHeadings: "## Setup\n\nRun the CLI to scaffold a new project.");
        var graph = NavigationGraphBuilder.Build(_root);
        var index = SearchIndex.FromGraph(graph);
        var page = index.Entries.First(e => e.Kind == SearchEntryKind.Page);
        Assert.NotNull(page.Body);
        Assert.Contains("Run the CLI", page.Body);
    }

    [Fact]
    public void FromGraph_HeadingEntriesHaveNullBody()
    {
        WritePage("intro.md", "Introduction", bodyHeadings: "## Setup\n\nBody prose.");
        var graph = NavigationGraphBuilder.Build(_root);
        var index = SearchIndex.FromGraph(graph);
        var heading = index.Entries.First(e => e.Kind == SearchEntryKind.Heading);
        Assert.Null(heading.Body);
    }
}
