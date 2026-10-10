using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using ShellDocs.Components;
using ShellDocs.Components.Chrome;
using ShellDocs.Components.Content;
using Xunit;

namespace ShellDocs.Tests;

public class CodeTabParserTests
{
    [Fact]
    public void ReadsLabelLanguageAndCode_FromFences_AndKeepsTheRest()
    {
        var raw = """

            <CodeTab Label="MAUI">
              ```xml
              <Button Text="Save" />
              ```
            </CodeTab>
            <span>kept</span>
            <CodeTab Label="Avalonia">
            ~~~csharp title
            var b = new Button();
            ~~~
            </CodeTab>
            """;

        var (tabs, rest) = CodeTabParser.Extract(raw);

        Assert.Equal(2, tabs.Count);
        Assert.Equal(new PreviewCodeTab("MAUI", "<Button Text=\"Save\" />", "xml"), tabs[0]);
        Assert.Equal(new PreviewCodeTab("Avalonia", "var b = new Button();", "csharp"), tabs[1]);
        Assert.Contains("<span>kept</span>", rest);
        Assert.DoesNotContain("CodeTab", rest);
    }

    [Fact]
    public void LanguageAttribute_AndPlainBody_AndNoTabs()
    {
        var (tabs, _) = CodeTabParser.Extract("<CodeTab Label=\"MAUI\" Language=\"XAML\">\n    <Label Text=\"Hi\" />\n</CodeTab><CodeTab>no label</CodeTab>");
        var tab = Assert.Single(tabs);
        Assert.Equal("xaml", tab.Language);
        Assert.Equal("<Label Text=\"Hi\" />", tab.Code);

        var (none, rest) = CodeTabParser.Extract("<Button>Go</Button>");
        Assert.Empty(none);
        Assert.Equal("<Button>Go</Button>", rest);
    }
}

public class PreviewCodeVariantsTests : IDisposable
{
    private readonly string _demoRoot;

    public PreviewCodeVariantsTests()
    {
        _demoRoot = Path.Combine(Path.GetTempPath(), "shelldocs-variants-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_demoRoot);
        File.WriteAllText(Path.Combine(_demoRoot, "SaveDemo.razor"), "<Button>Save</Button>");
    }

    public void Dispose()
    {
        try { Directory.Delete(_demoRoot, recursive: true); } catch { }
    }

    public class SaveDemo : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenElement(0, "span");
            b.AddAttribute(1, "class", "save-demo");
            b.CloseElement();
        }
    }

    private ComponentRenderHarness Harness(Action<ShellDocsOptions>? extra = null) => new(o =>
    {
        o.ContentRoot = Path.Combine(_demoRoot, "no-content");
        o.DemoSourceRoot = _demoRoot;
        o.RegisterComponent<Button>();
        o.RegisterComponent<SaveDemo>();
        extra?.Invoke(o);
    });

    private const string PlatformTabs = """
        <CodeTab Label="MAUI">
        ```xml
        <Button Text="Save" />
        ```
        </CodeTab>
        <CodeTab Label="Avalonia">
        ```xml
        <Button Content="Save" />
        ```
        </CodeTab>
        """;

    private static Dictionary<string, object?> Md(string markdown) => new() { ["Markdown"] = markdown };

    [Fact]
    public async Task DemoPreview_CodeTabs_ReplaceTheRazorSource()
    {
        var harness = Harness();
        var html = await harness.RenderAsync<MarkdownContent>(Md(
            "<DemoPreview Component=\"SaveDemo\" SyncKey=\"platform\" Caption=\"Rendered with ShellUI for Blazor\">\n" + PlatformTabs + "\n</DemoPreview>\n"));

        Assert.Contains("class=\"save-demo\"", html);
        Assert.DoesNotContain("&lt;Button&gt;Save&lt;/Button&gt;", html); // the Razor source is gone
        Assert.Matches("class=\"preview-code-variants\"[^>]*data-tabs data-tabs-missing data-tabs-value=\"MAUI\" data-tabs-sync=\"platform\"", html);
        Assert.Matches("aria-selected=\"true\" tabindex=\"0\"\\s+data-tab-target=\"MAUI\"", html);
        Assert.Matches("aria-selected=\"false\" tabindex=\"-1\"\\s+data-tab-target=\"Avalonia\"", html);
        Assert.Matches("data-tab-panel=\"Avalonia\" hidden", html);
        Assert.DoesNotMatch("data-tab-panel=\"MAUI\" hidden", html);
        Assert.Contains("&lt;Button Text=&quot;Save&quot; /&gt;", html);
        Assert.Contains("&lt;Button Content=&quot;Save&quot; /&gt;", html);
        Assert.Contains("class=\"language-xml\"", html);
        // [^>]* allows for the scoped-CSS attribute.
        Assert.Matches("data-tab-missing hidden[^>]*>\\s*Not available on <span data-tab-missing-label[^>]*></span> yet", html);
        Assert.Contains("class=\"preview-caption\" title=\"Rendered with ShellUI for Blazor\"", html);
        Assert.DoesNotContain(harness.Logs.Messages, m => m.Contains("skipped attribute 'ChildContent'"));
    }

    [Fact]
    public async Task DemoPreview_WithoutCodeTabs_ShowsTheRazorSource_AsBefore()
    {
        var html = await Harness().RenderAsync<DemoPreview>(new() { ["Component"] = "SaveDemo" });

        Assert.Contains("&lt;Button&gt;Save&lt;/Button&gt;", html);
        Assert.DoesNotContain("preview-code-variants", html);
        Assert.DoesNotContain("preview-caption", html);
        Assert.Contains("data-preview-tab-target=\"code\"", html);
    }

    [Fact]
    public async Task ComponentPreview_TakesCodeTabsOutOfTheTargetsChildren()
    {
        var html = await Harness().RenderAsync<MarkdownContent>(Md(
            "<ComponentPreview Component=\"Button\" SyncKey=\"platform\">Go\n" + PlatformTabs + "\n</ComponentPreview>\n"));

        Assert.Matches("<button data-variant=\"Default\"[^>]*>Go(\\s|&#xA;|&#xD;)*</button>", html);
        Assert.Contains("data-tab-target=\"Avalonia\"", html);
        Assert.DoesNotMatch("<button[^>]*data-variant[^>]*>[^<]*MAUI", html);
    }

    [Fact]
    public async Task Caption_DefaultsToTheSiteOption_AndAPreviewCanOverrideIt()
    {
        var harness = Harness(o => o.PreviewCaption = "Rendered with ShellUI for Blazor");

        var byDefault = await harness.RenderAsync<DemoPreview>(new() { ["Component"] = "SaveDemo" });
        Assert.Contains(">Rendered with ShellUI for Blazor</div>", byDefault);

        var own = await harness.RenderAsync<DemoPreview>(new() { ["Component"] = "SaveDemo", ["Caption"] = "Blazor" });
        Assert.Contains(">Blazor</div>", own);
    }

    [Fact]
    public async Task SyncSwitch_GoesUnderThePageHeader_OnPagesThatUseItsKey()
    {
        var harness = Harness(o =>
        {
            o.RenderPageTitle = true;
            o.AddSyncSwitch("platform", "MAUI", "Avalonia");
        });
        var demo = "<DemoPreview Component=\"SaveDemo\" SyncKey=\"platform\">\n" + PlatformTabs + "\n</DemoPreview>\n";

        var page = await harness.RenderAsync<MarkdownContent>(Md("---\ntitle: Button\ndescription: Saves.\n---\n\nIntro.\n\n" + demo));
        Assert.Matches("</header>\\s*<div class=\"sync-switch\"[^>]*data-tabs data-tabs-switch data-tabs-sync=\"platform\" data-tabs-value=\"MAUI\"", page);
        Assert.Matches("data-tab-target=\"Avalonia\"[^>]*>Avalonia</button>", page);
        Assert.Single(Regex.Matches(page, "data-tabs-switch"));
        Assert.DoesNotContain("blazor:onclick", page);

        // Without a page header it goes right above the first block that uses the key.
        var noHeader = await harness.RenderAsync<MarkdownContent>(Md("# Button\n\nIntro.\n\n" + demo));
        Assert.Matches("Intro.</p>\\s*<div class=\"sync-switch\"[^>]*data-tabs-switch", noHeader);

        // Placed by hand: not added a second time.
        var manual = await harness.RenderAsync<MarkdownContent>(Md("# Button\n\n<SyncSwitch SyncKey=\"platform\" />\n\n" + demo));
        Assert.Single(Regex.Matches(manual, "data-tabs-switch"));

        var otherKey = await harness.RenderAsync<MarkdownContent>(Md("---\ntitle: Button\n---\n\n<DemoPreview Component=\"SaveDemo\" />\n"));
        Assert.DoesNotContain("data-tabs-switch", otherKey);

        var chrome = await harness.RenderAsync<DocsHeader>();
        Assert.DoesNotContain("data-tabs-switch", chrome);

        var inline = await Harness().RenderAsync<SyncSwitch>(new() { ["SyncKey"] = "pm", ["Options"] = "npm, pnpm" });
        Assert.Contains("data-tab-target=\"pnpm\"", inline);

        Assert.Throws<ArgumentException>(() => new ShellDocsOptions().AddSyncSwitch("platform", "MAUI"));
    }

    [Fact]
    public async Task IframePreview_LeavesLoadingToShellDocsJs()
    {
        var html = await Harness().RenderAsync<IframePreview>(new() { ["Src"] = "/preview/?c=button" });

        Assert.Matches("class=\"iframe-preview\"[^>]*data-iframe-preview data-src=\"/preview/\\?c=button\" data-lazy=\"visible\" style=\"height:320px\"", html);
        Assert.DoesNotMatch("<iframe[^>]*\\ssrc=", html);
        Assert.Contains("data-layout=\"stretch\"", html);
        Assert.DoesNotContain("data-preview-tab-target=\"code\"", html); // no code tabs, no Code tab
        Assert.DoesNotContain("data-preview-copy", html);
        Assert.Contains("href=\"/preview/?c=button\"", html); // Open in new tab
        Assert.DoesNotContain("data-iframe-run", html);
    }

    [Fact]
    public async Task IframePreview_ClickMode_Height_CodeTabs_AndMissingSrc()
    {
        var harness = Harness();
        var html = await harness.RenderAsync<MarkdownContent>(Md(
            "<IframePreview Src=\"/preview/?c=save\" Height=\"24rem\" Lazy=\"click\" SyncKey=\"platform\">\n" + PlatformTabs + "\n</IframePreview>\n"));

        Assert.Contains("data-lazy=\"click\" style=\"height:24rem\"", html);
        Assert.Matches("data-iframe-run[^>]*>Run live preview</button>", html);
        Assert.Contains("data-preview-tab-target=\"code\"", html);
        Assert.Contains("data-tabs-sync=\"platform\"", html);
        Assert.Contains("data-tab-target=\"MAUI\"", html);
        Assert.DoesNotContain(harness.Logs.Messages, m => m.Contains("skipped attribute"));

        var missing = await harness.RenderAsync<IframePreview>(new());
        Assert.Contains("IframePreview needs a Src", missing);
    }

    [Fact]
    public void ShelldocsJs_ShowsTheMissingNote_AndLoadsIframesLazily()
    {
        var js = ReadAsset("shelldocs.js");

        Assert.Contains("root.hasAttribute('data-tabs-missing')", js);
        Assert.Contains("[data-tab-missing-label]", js);
        Assert.Contains("data-tabs-switch", js);
        Assert.Contains("url.searchParams.set('theme', theme())", js);
        Assert.Contains("type: 'shelldocs-theme'", js);
        Assert.Contains("xaml: 'xml'", js);
        Assert.Matches(new Regex("IntersectionObserver"), js);
    }

    private static string ReadAsset(string name)
    {
        var testDir = Path.GetDirectoryName(typeof(PreviewCodeVariantsTests).Assembly.Location)!;
        foreach (var candidate in new[]
        {
            Path.Combine(testDir, "wwwroot", "_content", "ShellDocs.Components", name),
            Path.Combine(testDir, "..", "..", "..", "..", "..", "src", "ShellDocs.Components", "wwwroot", name)
        })
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full)) return File.ReadAllText(full);
        }
        throw new FileNotFoundException(name);
    }
}
