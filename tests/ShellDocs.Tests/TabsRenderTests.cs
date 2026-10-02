using System.Text.RegularExpressions;
using ShellDocs.Components;
using ShellDocs.Components.Content;
using Xunit;

namespace ShellDocs.Tests;

// Server-rendered state of <Tabs> and <CodeGroup>: what a static host serves
// before shelldocs.js takes over the switching.
public class TabsRenderTests
{
    private static ComponentRenderHarness Harness() => new(o =>
        o.ContentRoot = Path.Combine(Path.GetTempPath(), "shelldocs-no-content-" + Guid.NewGuid().ToString("N")));

    private static Task<string> Render(string markdown)
        => Harness().RenderAsync<MarkdownContent>(new() { ["Markdown"] = markdown });

    [Fact]
    public async Task Tabs_RenderStripAndAllPanels_FirstTabSelected()
    {
        var html = await Render("<Tabs>\n<Tab Label=\"Blazor\">Server side.</Tab>\n<Tab Label=\"MAUI\">Native shell.</Tab>\n</Tabs>");

        Assert.Contains("data-tabs", html);
        Assert.Contains("data-tabs-value=\"Blazor\"", html);
        Assert.Contains("role=\"tablist\"", html);
        Assert.Matches("data-tab-target=\"Blazor\"[^>]*>Blazor<|aria-selected=\"true\" tabindex=\"0\"\\s+data-tab-target=\"Blazor\"", html);
        Assert.Contains("aria-selected=\"false\" tabindex=\"-1\"", html);
        Assert.Contains("Server side.", html);
        Assert.Contains("Native shell.", html); // every panel ships in the HTML
        Assert.Matches("data-tab-panel=\"MAUI\" hidden", html);
        Assert.DoesNotMatch("data-tab-panel=\"Blazor\" hidden", html);
        Assert.DoesNotContain("blazor:onclick", html);
    }

    [Fact]
    public async Task Tabs_DefaultValue_ValueAndSyncKey()
    {
        var html = await Render("<Tabs DefaultValue=\"pnpm\" SyncKey=\"pkg\">\n<Tab Label=\"npm\">a</Tab>\n<Tab Label=\"pnpm\" Value=\"pnpm\">b</Tab>\n<Tab Label=\"Yarn (classic)\" Value=\"yarn\">c</Tab>\n</Tabs>");

        Assert.Contains("data-tabs-value=\"pnpm\"", html);
        Assert.Contains("data-tabs-sync=\"pkg\"", html);
        Assert.Contains("data-tab-target=\"yarn\"", html);
        Assert.Contains(">Yarn (classic)</button>", html);
        Assert.Matches("data-tab-panel=\"npm\" hidden", html);
    }

    [Fact]
    public async Task Tabs_IdIsDeterministic_AndWiresAria()
    {
        const string md = "<Tabs>\n<Tab Label=\"A\">a</Tab>\n<Tab Label=\"B\">b</Tab>\n</Tabs>";
        var first = Regex.Match(await Render(md), "id=\"(tabs-[0-9a-f]{6})\"").Groups[1].Value;
        var second = Regex.Match(await Render(md), "id=\"(tabs-[0-9a-f]{6})\"").Groups[1].Value;

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
        var html = await Render(md);
        Assert.Contains($"aria-controls=\"{first}-panel-1\"", html);
        Assert.Contains($"aria-labelledby=\"{first}-tab-1\"", html);
    }

    [Fact]
    public async Task CodeGroup_UsesTheSameStaticContract()
    {
        var html = await Render("<CodeGroup SyncKey=\"pkg\">\n<CodeTab Label=\"npm\">npm i</CodeTab>\n<CodeTab Label=\"pnpm\">pnpm add</CodeTab>\n</CodeGroup>");

        Assert.Contains("class=\"code-group\"", html);
        Assert.Contains("data-tabs-sync=\"pkg\"", html);
        Assert.Contains("data-tabs-value=\"npm\"", html);
        Assert.Contains("data-tab-target=\"pnpm\"", html);
        Assert.Matches("data-tab-panel=\"pnpm\" hidden", html);
        Assert.Contains("pnpm add", html);
        Assert.DoesNotContain("blazor:onclick", html);
    }
}
