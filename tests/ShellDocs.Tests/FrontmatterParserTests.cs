using ShellDocs.Core;
using Xunit;

namespace ShellDocs.Tests;

public class FrontmatterParserTests
{
    [Fact]
    public void Parse_ExtractsFrontmatterAndBody()
    {
        var source = "---\ntitle: Button\ndescription: A clickable thing\norder: 5\n---\n# Button\n\nBody here.";

        var result = FrontmatterParser.Parse(source);

        Assert.Equal("Button", result.Frontmatter.GetValue<string>("title"));
        Assert.Equal("A clickable thing", result.Frontmatter.GetValue<string>("description"));
        Assert.Equal(5, result.Frontmatter.GetValue<int>("order"));
        Assert.Contains("# Button", result.Body);
        Assert.Contains("Body here.", result.Body);
    }

    [Fact]
    public void Parse_HandlesMissingFrontmatter()
    {
        var source = "# No Frontmatter\n\nJust markdown.";
        var result = FrontmatterParser.Parse(source);

        Assert.Empty(result.Frontmatter);
        Assert.Equal(source, result.Body);
    }

    [Fact]
    public void Parse_HandlesEmptyDocument()
    {
        var result = FrontmatterParser.Parse("");

        Assert.Empty(result.Frontmatter);
        Assert.Equal("", result.Body);
    }

    [Fact]
    public void Parse_HandlesUnclosedFrontmatter()
    {
        var source = "---\ntitle: Broken\n\n# Body";
        var result = FrontmatterParser.Parse(source);

        Assert.Empty(result.Frontmatter);
        Assert.Equal(source, result.Body);
    }

    [Fact]
    public void Parse_HandlesInvalidYaml()
    {
        var source = "---\ntitle: [unclosed\n---\n# Body";
        var result = FrontmatterParser.Parse(source);

        Assert.Empty(result.Frontmatter);
        Assert.Contains("# Body", result.Body);
    }

    [Fact]
    public void GetValue_CoercesTypes()
    {
        var source = "---\nage: 42\n---\n";
        var result = FrontmatterParser.Parse(source);

        Assert.Equal(42, result.Frontmatter.GetValue<int>("age"));
        Assert.Equal("42", result.Frontmatter.GetValue<string>("age"));
    }
}
