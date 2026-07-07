using ShellDocs.Core;
using Xunit;

namespace ShellDocs.Tests;

public class MetaJsonTests
{
    [Fact]
    public void Parse_HandlesFlatSlugList()
    {
        var json = """
        { "title": "Docs", "pages": ["intro", "installation", "theming"] }
        """;

        var meta = MetaJson.Parse(json);

        Assert.NotNull(meta);
        Assert.Equal("Docs", meta!.Title);
        Assert.Equal(3, meta.Pages.Count);
        Assert.All(meta.Pages, e => Assert.IsType<MetaJsonPageRef>(e));
        Assert.Equal("intro", ((MetaJsonPageRef)meta.Pages[0]).Slug);
    }

    [Fact]
    public void Parse_RecognizesDivider()
    {
        var json = """
        { "pages": ["a", "---", "b"] }
        """;

        var meta = MetaJson.Parse(json);

        Assert.NotNull(meta);
        Assert.IsType<MetaJsonPageRef>(meta!.Pages[0]);
        Assert.IsType<MetaJsonDivider>(meta.Pages[1]);
        Assert.IsType<MetaJsonPageRef>(meta.Pages[2]);
    }

    [Fact]
    public void Parse_HandlesSubsectionObject()
    {
        var json = """
        {
          "title": "Components",
          "pages": [
            "button",
            {
              "title": "Data Display",
              "pages": ["table", "card"]
            }
          ]
        }
        """;

        var meta = MetaJson.Parse(json);

        Assert.NotNull(meta);
        Assert.Equal(2, meta!.Pages.Count);
        var sub = Assert.IsType<MetaJsonSubsection>(meta.Pages[1]);
        Assert.Equal("Data Display", sub.Title);
        Assert.Equal(2, sub.Pages.Count);
        Assert.All(sub.Pages, e => Assert.IsType<MetaJsonPageRef>(e));
    }

    [Fact]
    public void Parse_ReturnsNullForBlank()
    {
        Assert.Null(MetaJson.Parse(""));
        Assert.Null(MetaJson.Parse("   "));
    }
}
