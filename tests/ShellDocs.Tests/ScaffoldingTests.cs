using ShellDocs.Core;
using ShellDocs.Markdown;
using ShellDocs.Templates;
using Xunit;

namespace ShellDocs.Tests;

// Smoke tests — verify each package's public surface loads and its trivial APIs behave.
// Real tests land branch-by-branch.
public class ScaffoldingTests
{
    [Fact]
    public void NavigationNode_HoldsRequiredFields()
    {
        var node = new NavigationNode { Url = "/docs/hello", Title = "Hello" };
        Assert.Equal("/docs/hello", node.Url);
        Assert.Equal("Hello", node.Title);
    }

    [Fact]
    public void MarkdownPipelineFactory_ProducesUsablePipeline()
    {
        var pipeline = MarkdownPipelineFactory.Create();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void StarterPageTemplate_EmitsFrontmatterAndBody()
    {
        var content = StarterPageTemplate.Content("Introduction", "Getting started with ShellDocs");
        Assert.Contains("title: Introduction", content);
        Assert.Contains("description: Getting started with ShellDocs", content);
        Assert.Contains("# Introduction", content);
    }
}
