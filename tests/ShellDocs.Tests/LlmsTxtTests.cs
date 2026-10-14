using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShellDocs.Components;
using ShellDocs.Components.Chrome;
using ShellDocs.Components.Content;
using ShellDocs.Components.Llms;
using ShellDocs.Core;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class Shout : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
}

public class LlmsTxtTests : IDisposable
{
    private const string Site = "https://shellui.dev";
    private readonly string _root;

    public LlmsTxtTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-llms-" + Guid.NewGuid().ToString("N"));
        Write("content/docs/introduction.md", "---\ntitle: Introduction\ndescription: Start here.\n---\nRead the [setup](/docs/guides/setup#install).\n");
        Write("content/docs/guides/setup.md", "---\ntitle: Setup\ndescription: Set it up\n---\n# Setup\n\n```bash\nshellui add button\n```\n");
        Write("content/docs/v2/button.md", "---\ntitle: Button\n---\n# Button\n\nThe new button.\n");
        Write("content/docs/v1/button.md", "---\ntitle: Button\n---\n# Button\n\nThe old button.\n");
        Write("rules.md", "## Rules for AI agents\n\n1. Use ShellUI.\n");
        Write("demos/HelloDemo.razor", "<p>Hello</p>\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private ComponentRenderHarness Harness(Action<LlmsTxtOptions>? llms = null, bool enabled = true, string uri = "http://localhost/docs/introduction") => new(o =>
    {
        o.ContentRoot = Path.Combine(_root, "content");
        o.SiteName = "ShellUI";
        o.SiteTagline = "ShellUI for Blazor";
        o.DemoSourceRoot = Path.Combine(_root, "demos");
        o.RegisterComponent<Shout>();
        o.AddVersion("v2", "v2.0", "/docs/v2", latest: true);
        o.AddVersion("v1", "v1.0", "/docs/v1");
        if (enabled)
            o.EnableLlmsTxt(l =>
            {
                l.RulesFile = Path.Combine(_root, "rules.md");
                l.InstallCommandPattern = @"shellui add [\w-]+";
                llms?.Invoke(l);
            });
    }, uri);

    private LlmsMarkdown Converter(ComponentRenderHarness h) => new(
        h.Services.GetRequiredService<TypeRegistry>(), h.Services.GetRequiredService<NavigationGraph>(), Path.Combine(_root, "demos"), Site);

    [Fact]
    public void EveryBuiltInComponent_HasAMarkdownForm()
    {
        var builtIns = Harness().Services.GetRequiredService<ShellDocsOptions>().BuiltInComponents;
        Assert.NotEmpty(builtIns);
        Assert.All(builtIns, t => Assert.Contains(t, LlmsMarkdown.HandledTypes));
    }

    [Fact]
    public void Convert_TurnsEveryDocsTagIntoMarkdown()
    {
        const string page = """
            <DocsCallout Variant="warning" Title="Careful">
            Text with [link](/docs/guides/setup).

            ```bash
            run it
            ```
            </DocsCallout>

            <DocsCallout Text="One line." />

            <DocsTabs>
            <DocsTab Label="One">

            First.

            </DocsTab>
            <DocsTab Label="Two">Second.</DocsTab>
            </DocsTabs>

            <DocsCodeGroup>
            <DocsCodeTab Label="C#">

            ```csharp
            var x = 1;
            ```

            </DocsCodeTab>
            </DocsCodeGroup>

            <DocsCardGrid>
            <DocsLinkCard Title="Setup" Href="/docs/guides/setup" Description="Set it up." />
            <DocsCard Title="Plain">Card body.</DocsCard>
            </DocsCardGrid>

            <DocsSteps>
            <DocsStep Title="Install">Get it.</DocsStep>
            <DocsStep Title="Run">Go.</DocsStep>
            </DocsSteps>

            <DocsFileTree>
            <DocsFileTreeItem Name="src" IsFolder="true">
            <DocsFileTreeItem Name="App.razor" Comment="root" />
            </DocsFileTreeItem>
            </DocsFileTree>

            <DocsTypeTable>
            <DocsTypeRow Name="Title" Type="string" Default="null" Description="The title." Required="true" />
            </DocsTypeTable>

            <DocsAutoTypeTable Component="DocsCallout" />

            <DocsDemoPreview Component="HelloDemo" Title="Hello" />

            <DocsComponentPreview Component="DocsCallout" Variant="tip" Caption="Note">Hi</DocsComponentPreview>

            <DocsIframePreview Src="/preview/index.html" />

            <DocsSyncSwitch SyncKey="platform" Options="A, B" />

            ```razor:preview
            <DocsCallout Text="Live" />
            ```

            ````markdown
            <DocsCallout Text="Shown as code" />
            ````

            Inline <Shout>loud</Shout> word, and `<DocsCallout>` in a code span.
            """;
        var md = Converter(Harness()).Convert(page);

        Assert.DoesNotMatch("<Docs", OutsideCode(md));
        Assert.Contains("> **Careful**\n>\n> Text with [link](https://shellui.dev/docs/guides/setup.md).\n>\n> ```bash\n> run it\n> ```", md);
        Assert.Contains("> **Note**\n>\n> One line.", md);
        Assert.Contains("**One**\n\nFirst.", md);
        Assert.Contains("**Two**\n\nSecond.", md);
        Assert.Contains("**C#**\n\n```csharp\nvar x = 1;\n```", md);
        Assert.Contains("- [Setup](https://shellui.dev/docs/guides/setup.md): Set it up.\n- **Plain**: Card body.", md);
        Assert.Contains("**1. Install**\n\nGet it.", md);
        Assert.Contains("**2. Run**\n\nGo.", md);
        Assert.Contains("```text\nsrc/\n  App.razor  # root\n```", md);
        Assert.Contains("| `Title` (required) | `string` | `null` | The title. |", md);
        Assert.Contains("| `Variant` | `string` |", md);
        Assert.Contains("**Hello**\n\n```razor\n<p>Hello</p>\n```", md);
        Assert.Contains("```razor\n<DocsCallout Variant=\"tip\">Hi</DocsCallout>\n```", md);
        Assert.Contains("[Live preview](https://shellui.dev/preview/index.html)", md);
        Assert.DoesNotContain("platform", md);
        Assert.Contains("```razor\n<DocsCallout Text=\"Live\" />\n```", md);
        Assert.Contains("````markdown\n<DocsCallout Text=\"Shown as code\" />\n````", md);
        Assert.Contains("Inline loud word, and `<DocsCallout>` in a code span.", md);
    }

    // The text outside fences (quoted ones too) and code spans.
    private static string OutsideCode(string md)
    {
        var kept = new List<string>();
        string? fence = null;
        foreach (var line in md.Split('\n'))
        {
            var t = line.TrimStart('>', ' ');
            var run = Regex.Match(t, "^`{3,}").Value;
            if (fence is null && run.Length > 0) { fence = run; continue; }
            if (fence is not null) { if (t.StartsWith(fence) && t.Trim('`').Length == 0) fence = null; continue; }
            kept.Add(Regex.Replace(line, "`[^`\n]*`", ""));
        }
        return string.Join("\n", kept);
    }

    [Fact]
    public void Index_ListsTheLatestPages_WithRulesAndInstallCommands()
    {
        var files = Harness().Services.GetRequiredService<LlmsTxtFiles>();
        var index = files.Get("/llms.txt", Site)!.Body;

        Assert.StartsWith("# ShellUI\n\n> ShellUI for Blazor. These docs are for v2.0. Other versions: [v1.0](https://shellui.dev/docs/v1/llms.txt).\n\n## Rules for AI agents\n\n1. Use ShellUI.\n\n", index);
        Assert.Contains("The [full text](https://shellui.dev/llms-full.txt) has every page below in one file.", index);
        Assert.Contains("- [Introduction](https://shellui.dev/docs/introduction.md): Start here.\n", index);
        Assert.Contains("- [Setup](https://shellui.dev/docs/guides/setup.md): Set it up. Install: `shellui add button`\n", index);
        Assert.Contains("- [Button](https://shellui.dev/docs/v2/button.md)", index);
        Assert.DoesNotContain("/docs/v1/button", index);

        var v1 = files.Get("/docs/v1/llms.txt", Site)!.Body;
        Assert.Contains("These docs are for v1.0. Other versions: [v2.0](https://shellui.dev/docs/v2/llms.txt).", v1);
        Assert.Contains("- [Button](https://shellui.dev/docs/v1/button.md)", v1);
        Assert.DoesNotContain("introduction", v1);
    }

    [Fact]
    public void FullText_AndPages_AreMarkdown()
    {
        var files = Harness().Services.GetRequiredService<LlmsTxtFiles>();

        var page = files.Get("/docs/introduction.md", Site)!;
        Assert.False(page.IsDownload);
        Assert.Equal("# Introduction\n\n> Start here.\n\nSource: https://shellui.dev/docs/introduction\n\nRead the [setup](https://shellui.dev/docs/guides/setup.md#install).\n", page.Body);
        // A page with its own heading keeps it.
        Assert.StartsWith("# Setup\n\n> Set it up\n\nSource: https://shellui.dev/docs/guides/setup\n\n```bash", files.Get("/docs/guides/setup.md", Site)!.Body);

        var full = files.Get("/llms-full.txt", Site)!.Body;
        Assert.Contains("\n\n---\n\n# Introduction\n\n> Start here.", full);
        Assert.Contains("\n\n---\n\n# Button\n\n", full);
        Assert.Contains("The new button.", full);
        Assert.DoesNotContain("The old button.", full);

        Assert.Null(files.Get("/docs/nope.md", Site));
        Assert.Null(files.Get("/docs/introduction", Site));
        // Root-relative links when there is no base URL.
        Assert.Contains("[setup](/docs/guides/setup.md#install)", files.Get("/docs/introduction.md", "")!.Body);
    }

    [Fact]
    public void AgentFiles_AreASectionToAppend()
    {
        var files = Harness().Services.GetRequiredService<LlmsTxtFiles>();

        var claude = files.Get("/agents/CLAUDE.md", Site)!;
        Assert.True(claude.IsDownload);
        Assert.StartsWith("\n## ShellUI\n\nThis project uses ShellUI v2.0. Before writing code that uses it, read https://shellui.dev/llms.txt: it lists every docs page with its install command.", claude.Body);
        Assert.Contains("### Rules for AI agents", claude.Body);

        Assert.StartsWith("---\ndescription: ShellUI for Blazor\nalwaysApply: true\n---\n\n## ShellUI\n\n", files.Get("/agents/shellui.mdc", Site)!.Body);
        Assert.Contains("read https://shellui.dev/docs/v1/llms.txt", files.Get("/docs/v1/agents/AGENTS.md", Site)!.Body);
        Assert.NotNull(files.Get("/docs/v2/agents/copilot-instructions.md", Site));

        var off = Harness(l => l.AgentFiles = false).Services.GetRequiredService<LlmsTxtFiles>();
        Assert.Null(off.Get("/agents/CLAUDE.md", Site));
        Assert.DoesNotContain(off.AllPaths(), p => p.Contains("/agents/"));
    }

    [Fact]
    public void AllPaths_ListsEveryFileForTheBuild()
    {
        var paths = Harness().Services.GetRequiredService<LlmsTxtFiles>().AllPaths().ToList();
        foreach (var expected in new[]
        {
            "/llms.txt", "/llms-full.txt", "/agents/AGENTS.md", "/agents/shellui.mdc",
            "/docs/v1/llms.txt", "/docs/v2/llms-full.txt", "/docs/v2/agents/CLAUDE.md",
            "/docs/introduction.md", "/docs/guides/setup.md", "/docs/v1/button.md",
        })
            Assert.Contains(expected, paths);
    }

    [Fact]
    public async Task Middleware_ServesTextAndDownloads_AndPassesOnTheRest()
    {
        var services = Harness().Services;
        var passedOn = 0;
        var middleware = new LlmsTxtMiddleware(_ => { passedOn++; return Task.CompletedTask; },
            services.GetRequiredService<LlmsTxtFiles>(), services.GetRequiredService<ShellDocsOptions>());

        var txt = Context("/llms.txt");
        await middleware.InvokeAsync(txt);
        Assert.Equal("text/plain; charset=utf-8", txt.Response.ContentType);
        Assert.Contains("(https://docs.test/docs/introduction.md)", Body(txt));
        Assert.False(txt.Response.Headers.ContainsKey("Content-Disposition"));

        var agent = Context("/agents/AGENTS.md");
        await middleware.InvokeAsync(agent);
        Assert.Equal("attachment; filename=\"AGENTS.md\"", agent.Response.Headers.ContentDisposition.ToString());

        // shelldocs build, from this machine: the export's site URL or base path.
        var export = Context("/docs/introduction.md");
        export.Request.Headers[LlmsTxtMiddleware.ExportSiteUrlHeader] = "";
        export.Request.Headers[LlmsTxtMiddleware.ExportBasePathHeader] = "/sub";
        await middleware.InvokeAsync(export);
        Assert.Contains("(/sub/docs/guides/setup.md#install)", Body(export));

        // The same headers from anywhere else are ignored.
        var remote = Context("/docs/introduction.md");
        remote.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");
        remote.Request.Headers[LlmsTxtMiddleware.ExportSiteUrlHeader] = "https://evil.test";
        await middleware.InvokeAsync(remote);
        Assert.Contains("(https://docs.test/docs/guides/setup.md#install)", Body(remote));

        await middleware.InvokeAsync(Context("/docs/introduction"));
        await middleware.InvokeAsync(Context("/docs/nope.md"));
        Assert.Equal(2, passedOn);
    }

    [Fact]
    public void Middleware_IsOnlyInThePipelineWhenEnabled()
    {
        Assert.Contains(Harness().Services.GetServices<IStartupFilter>(), f => f is LlmsTxtStartupFilter);
        Assert.DoesNotContain(Harness(enabled: false).Services.GetServices<IStartupFilter>(), f => f is LlmsTxtStartupFilter);
        Assert.Null(Harness(enabled: false).Services.GetService<LlmsTxtFiles>());
    }

    [Fact]
    public async Task PageActions_AreStaticHostFriendly()
    {
        var html = await Harness().RenderAsync<DocsPageActions>();
        Assert.Contains("data-page-md=\"docs/introduction.md\"", html);
        Assert.Matches("<button[^>]*data-page-copy", html);
        Assert.Matches("<a[^>]*href=\"docs/introduction.md\"[^>]*target=\"_blank\"[^>]*>[\\s\\S]*?View as Markdown", html);
        Assert.Contains("data-page-ai=\"https://claude.ai/new?q=\"", html);
        Assert.Contains("data-page-ai=\"https://chatgpt.com/?q=\"", html);
        Assert.Contains("href=\"https://claude.ai/new?q=Read%20%2Fdocs%2Fintroduction.md%20and%20help%20me%20with%20it.\"", html);
        Assert.DoesNotContain("blazor:on", html);

        Assert.Contains("data-page-md", await Harness().RenderAsync<MarkdownContent>(new() { ["Markdown"] = "# Hi" }));
        Assert.DoesNotContain("page-actions", await Harness(enabled: false).RenderAsync<DocsPageActions>());
        Assert.DoesNotContain("page-actions", await Harness(l => l.PageActions = false).RenderAsync<DocsPageActions>());
        Assert.DoesNotContain("page-actions", await Harness(uri: "http://localhost/showcase").RenderAsync<DocsPageActions>());
    }

    [Fact]
    public void ShelldocsJs_DrivesThePageMenu_AndLoadsFileLinks()
    {
        var js = File.ReadAllText(FindAsset("shelldocs.js"));
        Assert.Contains("'.pkg, .ver, .preview-menu, .page-actions'", js);
        Assert.Contains("[data-page-copy]", js);
        Assert.Contains("[data-page-ai]", js);
        Assert.Contains("/\\.(txt|md|mdc)$/i", js);
    }

    private static DefaultHttpContext Context(string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("docs.test");
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static string Body(HttpContext ctx) => System.Text.Encoding.UTF8.GetString(((MemoryStream)ctx.Response.Body).ToArray());

    private static string FindAsset(string name)
    {
        var testDir = Path.GetDirectoryName(typeof(LlmsTxtTests).Assembly.Location)!;
        foreach (var candidate in new[]
        {
            Path.Combine(testDir, "wwwroot", "_content", "ShellDocs.Components", name),
            Path.Combine(testDir, "..", "..", "..", "..", "..", "src", "ShellDocs.Components", "wwwroot", name)
        })
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full)) return full;
        }
        throw new FileNotFoundException(name);
    }
}
