using ShellDocs.Core;
using Xunit;

namespace ShellDocs.Tests;

public class MarkdownPlainTextTests
{
    [Fact]
    public void Extract_StripsFrontmatter()
    {
        var text = MarkdownPlainText.Extract("---\ntitle: Foo\n---\nHello world");
        Assert.Equal("Hello world", text);
    }

    [Fact]
    public void Extract_StripsFencedCodeBlocks()
    {
        var md = "Prose before.\n\n```csharp\nvar x = 1;\n```\n\nProse after.";
        var text = MarkdownPlainText.Extract(md);
        Assert.Contains("Prose before", text);
        Assert.Contains("Prose after", text);
        Assert.DoesNotContain("var x = 1", text);
    }

    [Fact]
    public void Extract_StripsRazorComponentTags()
    {
        var text = MarkdownPlainText.Extract("Before <Callout Title=\"x\">body</Callout> after.");
        Assert.DoesNotContain("<Callout", text);
        Assert.Contains("body", text);
        Assert.Contains("Before", text);
        Assert.Contains("after", text);
    }

    [Fact]
    public void Extract_UnwrapsLinksAndImages()
    {
        var text = MarkdownPlainText.Extract("See [our docs](https://x.com) and ![alt](/img.png).");
        Assert.Contains("our docs", text);
        Assert.Contains("alt", text);
        Assert.DoesNotContain("https://", text);
        Assert.DoesNotContain("img.png", text);
    }

    [Fact]
    public void Extract_UnwrapsEmphasisAndInlineCode()
    {
        var text = MarkdownPlainText.Extract("This is **bold**, *italic*, and `code`.");
        Assert.Contains("bold", text);
        Assert.Contains("italic", text);
        Assert.Contains("code", text);
        Assert.DoesNotContain("**", text);
        Assert.DoesNotContain("`", text);
    }

    [Fact]
    public void Extract_StripsHeadingHashesButKeepsText()
    {
        var text = MarkdownPlainText.Extract("## Setup\n\n### Install\n\nRun the CLI.");
        Assert.Contains("Setup", text);
        Assert.Contains("Install", text);
        Assert.Contains("Run the CLI", text);
        Assert.DoesNotContain("##", text);
    }

    [Fact]
    public void Extract_TrimsAtMaxLength()
    {
        var long_ = new string('a', 10_000);
        var text = MarkdownPlainText.Extract(long_, maxLength: 500);
        Assert.Equal(500, text.Length);
    }

    [Fact]
    public void Extract_HandlesEmptyInput()
    {
        Assert.Equal("", MarkdownPlainText.Extract(""));
        Assert.Equal("", MarkdownPlainText.Extract(null!));
    }
}
