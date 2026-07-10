using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class HeadingExtractorTests
{
    private static IReadOnlyList<Core.Heading> Extract(string md) =>
        new MarkdownRenderer().Render(md).Headings;

    [Fact]
    public void Extract_ReadsLevelAndText()
    {
        var headings = Extract("# One\n## Two\n### Three");
        Assert.Equal(3, headings.Count);
        Assert.Equal(1, headings[0].Level);
        Assert.Equal("One", headings[0].Text);
        Assert.Equal(2, headings[1].Level);
        Assert.Equal(3, headings[2].Level);
    }

    [Fact]
    public void Extract_SlugifiesText()
    {
        var headings = Extract("## Getting Started\n\n## API Reference\n\n### Prop `Value`");
        Assert.Equal("getting-started", headings[0].Id);
        Assert.Equal("api-reference", headings[1].Id);
        Assert.Equal("prop-value", headings[2].Id);
    }

    [Fact]
    public void Extract_DisambiguatesDuplicateSlugs()
    {
        var headings = Extract("## Setup\n\n## Setup\n\n## Setup");
        Assert.Equal("setup", headings[0].Id);
        Assert.Equal("setup-2", headings[1].Id);
        Assert.Equal("setup-3", headings[2].Id);
    }

    [Fact]
    public void Extract_HandlesInlineFormattingInHeading()
    {
        var headings = Extract("## Use **`Button`** with care");
        Assert.Equal("Use Button with care", headings[0].Text);
        Assert.Equal("use-button-with-care", headings[0].Id);
    }

    [Fact]
    public void Extract_EmptyDocument_ReturnsEmpty()
    {
        var headings = Extract("Just a paragraph.");
        Assert.Empty(headings);
    }
}
