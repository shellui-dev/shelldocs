using ShellDocs.Components;
using ShellDocs.Components.Content;
using Xunit;

namespace ShellDocs.Tests;

public class TypeTableRenderTests
{
    private static Task<string> Render(string markdown)
        => new ComponentRenderHarness(o =>
                o.ContentRoot = Path.Combine(Path.GetTempPath(), "shelldocs-no-content-" + Guid.NewGuid().ToString("N")))
            .RenderAsync<MarkdownContent>(new() { ["Markdown"] = markdown });

    [Fact]
    public async Task RowsThatShareAName_AreAllRendered()
    {
        var html = await Render(
            "<TypeTable>\n<TypeRow Name=\"SetLogo\" Type=\"method\" Description=\"One image.\" />\n" +
            "<TypeRow Name=\"SetLogo\" Type=\"method\" Description=\"An image per theme.\" />\n</TypeTable>");

        Assert.Contains("One image.", html);
        Assert.Contains("An image per theme.", html);
    }
}
