using ShellDocs.Markdown;
using ShellDocs.Templates;
using Xunit;

namespace ShellDocs.Tests;

public class ScaffoldingTests
{
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
