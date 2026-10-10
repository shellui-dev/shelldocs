using System.Text.RegularExpressions;
using ShellDocs.Components.Content;
using Xunit;

namespace ShellDocs.Tests;

public class InlineChildContentTests
{
    private static Task<string> Render(string markdown) => new ComponentRenderHarness(o =>
    {
        o.ContentRoot = Path.Combine(Path.GetTempPath(), "shelldocs-no-content-" + Guid.NewGuid().ToString("N"));
        o.RegisterComponent<Badge>();
    }).RenderAsync<MarkdownContent>(new() { ["Markdown"] = markdown });

    [Fact]
    public async Task BodyOnTheTagsLine_StaysInline()
    {
        var html = await Render("Status: <Badge>v0.3.2 **stable**</Badge> and more.\n");

        Assert.Matches("<span[^>]*>v0.3.2 <strong>stable</strong></span>", html);
        Assert.DoesNotMatch("<span[^>]*><p>", html);
    }

    [Fact]
    public async Task BodyOnItsOwnLines_KeepsParagraphs()
    {
        var html = await Render("<Badge>\nFirst paragraph.\n\nSecond paragraph.\n</Badge>\n");

        Assert.Contains("<p>First paragraph.</p>", html);
        Assert.Contains("<p>Second paragraph.</p>", html);
    }

    [Theory]
    [InlineData("<p>one</p>\n", "one")]
    [InlineData("<p>a</p>\n<p>b</p>\n", "<p>a</p>\n<p>b</p>\n")]
    [InlineData("<ul><li>x</li></ul>", "<ul><li>x</li></ul>")]
    public void Unwrap_OnlyStripsASingleParagraph(string html, string expected)
        => Assert.Equal(expected, SlotRenderer.Unwrap(html));
}
