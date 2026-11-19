using ShellDocs.Core;
using Xunit;

namespace ShellDocs.Tests;

public class SlugTitleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "shelldocs-slugtitle-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Theory]
    [InlineData("getting-started", "Getting Started")]
    [InlineData("cli", "CLI")]
    [InlineData("api-reference", "API Reference")]
    [InlineData("rest-apis", "Rest APIs")]
    [InlineData("github-oauth", "GitHub OAuth")]
    [InlineData("ios-and-macos", "iOS And macOS")]
    [InlineData("llms-txt", "LLMs Txt")]
    [InlineData("shellUI", "ShellUI")]
    public void FromSlug_SpellsKnownWords(string slug, string expected)
    {
        Assert.Equal(expected, SlugTitle.FromSlug(slug));
    }

    [Fact]
    public void FromSlug_KeepsEmptySegments()
    {
        Assert.Equal("A  B", SlugTitle.FromSlug("a--b"));
    }

    [Fact]
    public void NavigationGraph_TitlesFoldersAndPagesWithKnownWords()
    {
        foreach (var path in new[] { "cli/build.md", "api/oauth-setup.md" })
        {
            var full = Path.Combine(_root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "# Page\n");
        }

        var graph = NavigationGraphBuilder.Build(_root);
        var sections = graph.Root.Children.Select(c => c.Title).ToList();

        Assert.Equal(new[] { "API", "CLI" }, sections);
        Assert.Equal("OAuth Setup", graph.ResolveByUrl("/api/oauth-setup")!.Title);
    }
}
