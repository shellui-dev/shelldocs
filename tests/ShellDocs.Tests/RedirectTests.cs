using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShellDocs.Components;
using ShellDocs.Core;
using Xunit;

namespace ShellDocs.Tests;

// content/docs: introduction, guides/{setup,deploy}, and versions v1 (older) and
// v2.0 (latest), ordered by meta.json. Some pages exist only in v2.0.
public class RedirectTests : IDisposable
{
    private readonly string _root;

    public RedirectTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-redirect-" + Guid.NewGuid().ToString("N"));
        Write("docs/introduction.md", "Intro");
        Write("docs/guides/setup.md", "Setup");
        Write("docs/guides/deploy.md", "Deploy");
        foreach (var v in new[] { "v1", "v2.0" })
        {
            Write($"docs/{v}/getting-started.md", $"Start {v}");
            Write($"docs/{v}/cli/build.md", $"Build {v}");
        }
        Write("docs/v2.0/cli/dev.md", "Dev");
        Meta("docs", "introduction", "guides");
        Meta("docs/guides", "setup", "deploy");
        Meta("docs/v1", "getting-started", "cli");
        Meta("docs/v2.0", "getting-started", "cli");
    }

    private void Meta(string folder, params string[] pages)
        => File.WriteAllText(Path.Combine(_root, folder.Replace('/', Path.DirectorySeparatorChar), "meta.json"),
            JsonSerializer.Serialize(new { pages }));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void Write(string rel, string body)
    {
        var path = Path.Combine(_root, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"---\ntitle: {Path.GetFileNameWithoutExtension(rel)}\n---\n{body}\n");
    }

    private DocsRedirects Redirects(Action<ShellDocsOptions>? configure = null)
    {
        var options = new ShellDocsOptions { ContentRoot = _root };
        options.AddVersion("v2.0", "v2.0", "/docs/v2.0", "Latest", latest: true);
        options.AddVersion("v1", "v1", "/docs/v1", "Older");
        configure?.Invoke(options);
        var graph = NavigationGraphBuilder.Build(_root);
        return new DocsRedirects(options, graph, new DocsVersionResolver(options, graph));
    }

    [Fact]
    public void VersionRoots_GoToTheVersionsFirstPage_IncludingDottedOnes()
    {
        var r = Redirects();
        Assert.Equal("/docs/v2.0/getting-started", r.Resolve("/docs/v2.0")?.Url);
        Assert.Equal("/docs/v1/getting-started", r.Resolve("/docs/v1/")?.Url);
        Assert.False(r.Resolve("/docs/v2.0")!.Permanent);
    }

    [Fact]
    public void Folders_GoToTheirFirstPage_AndTheDocsRootPrefersUnversionedPages()
    {
        var r = Redirects();
        Assert.Equal("/docs/guides/setup", r.Resolve("/docs/guides")?.Url);
        Assert.Equal("/docs/v2.0/cli/build", r.Resolve("/docs/v2.0/cli")?.Url);
        Assert.Equal("/docs/introduction", r.Resolve("/docs")?.Url);
    }

    [Fact]
    public void DocsRoot_FallsBackToTheLatestVersion_WhenEverythingIsVersioned()
    {
        File.Delete(Path.Combine(_root, "docs", "introduction.md"));
        Directory.Delete(Path.Combine(_root, "docs", "guides"), recursive: true);

        Assert.Equal("/docs/v2.0/getting-started", Redirects().Resolve("/docs")?.Url);
    }

    [Fact]
    public void UnversionedUrls_GoToTheLatestVersion()
    {
        var r = Redirects();
        Assert.Equal("/docs/v2.0/cli/dev", r.Resolve("/docs/cli/dev")?.Url);
        Assert.Equal("/docs/v2.0/getting-started", r.Resolve("/docs/getting-started")?.Url);
        Assert.Equal("/docs/v2.0/cli/build", r.Resolve("/docs/cli")?.Url);
    }

    [Fact]
    public void Pages_AndUnknownUrls_AreLeftAlone()
    {
        var r = Redirects();
        Assert.Null(r.Resolve("/docs/introduction"));
        Assert.Null(r.Resolve("/docs/v1/cli/build"));
        Assert.Null(r.Resolve("/docs/nope"));
        Assert.Null(r.Resolve("/"));
        Assert.Null(r.Resolve("/_framework/blazor.web.js"));
    }

    [Fact]
    public void Rules_ArePrefixes_AndChainIntoComputedRedirects()
    {
        var r = Redirects(o => o.AddRedirect("/docs/v2.0.0", "/docs/v2.0").AddRedirect("/docs", "/docs/v2.0", permanent: false));

        var page = r.Resolve("/docs/v2.0.0/cli/dev");
        Assert.Equal("/docs/v2.0/cli/dev", page?.Url);
        Assert.True(page!.Permanent);

        var root = r.Resolve("/docs/v2.0.0");
        Assert.Equal("/docs/v2.0/getting-started", root?.Url);
        Assert.False(root!.Permanent);

        // A rule whose target is inside its source doesn't loop.
        Assert.Null(r.Resolve("/docs/v2.0/cli/dev"));
    }

    [Fact]
    public void All_ListsComputedAndRuleRedirects_AndNeverAPage()
    {
        var r = Redirects(o => o.AddRedirect("/docs/old", "/docs/guides"));
        var all = r.All().ToDictionary(x => x.From, x => x.To, StringComparer.OrdinalIgnoreCase);

        Assert.Equal("/docs/v2.0/getting-started", all["/docs/v2.0"]);
        Assert.Equal("/docs/v2.0/cli/dev", all["/docs/cli/dev"]);
        Assert.Equal("/docs/guides/deploy", all["/docs/old/deploy"]);
        Assert.DoesNotContain("/docs/introduction", all.Keys);
        Assert.DoesNotContain("/docs/v1/cli/build", all.Keys);
    }

    [Fact]
    public async Task Middleware_Redirects_AndServesTheMap()
    {
        var r = Redirects(o => o.AddRedirect("/docs/old", "/docs/guides"));
        var reachedNext = false;
        var middleware = Activator.CreateInstance(MiddlewareType(), (RequestDelegate)(_ => { reachedNext = true; return Task.CompletedTask; }), r)!;
        var invoke = MiddlewareType().GetMethod("InvokeAsync")!;

        var folder = Context("/docs/v2.0", "?tab=1", "/base");
        await (Task)invoke.Invoke(middleware, [folder])!;
        Assert.Equal(302, folder.Response.StatusCode);
        Assert.Equal("/base/docs/v2.0/getting-started?tab=1", folder.Response.Headers.Location.ToString());

        var moved = Context("/docs/old/setup");
        await (Task)invoke.Invoke(middleware, [moved])!;
        Assert.Equal(301, moved.Response.StatusCode);

        var page = Context("/docs/introduction");
        await (Task)invoke.Invoke(middleware, [page])!;
        Assert.True(reachedNext);

        var map = Context("/_shelldocs/redirects.json");
        map.Response.Body = new MemoryStream();
        await (Task)invoke.Invoke(middleware, [map])!;
        map.Response.Body.Position = 0;
        using var json = JsonDocument.Parse(map.Response.Body);
        Assert.Contains(json.RootElement.EnumerateArray(), e => e.GetProperty("from").GetString() == "/docs/v2.0");
    }

    [Fact]
    public void AddShellDocs_RegistersTheStartupFilter_UnlessDisabled()
    {
        var on = new ServiceCollection().AddShellDocs(o => o.ContentRoot = _root);
        Assert.Contains(on, d => d.ServiceType == typeof(IStartupFilter));

        var off = new ServiceCollection().AddShellDocs(o => { o.ContentRoot = _root; o.EnableRedirects = false; });
        Assert.DoesNotContain(off, d => d.ServiceType == typeof(IStartupFilter));
    }

    [Fact]
    public void BuildRedirectPage_IsBaseRelative()
    {
        var cli = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "shelldocs") ?? Assembly.Load("shelldocs");
        var page = (string)cli.GetType("ShellDocs.CLI.Commands.PrerenderRunner", throwOnError: true)!
            .GetMethod("RedirectPage", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
            .Invoke(null, ["/docs/v2.0/getting-started"])!;

        Assert.Contains("<base href=\"/\" />", page);
        Assert.Contains("url=docs/v2.0/getting-started", page);
        Assert.Contains("\"docs/v2.0/getting-started\"", page);
        Assert.Contains("noindex", page);
    }

    private static Type MiddlewareType() => typeof(DocsRedirects).Assembly.GetType("ShellDocs.Components.DocsRedirectMiddleware", throwOnError: true)!;

    private static DefaultHttpContext Context(string path, string query = "", string pathBase = "")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = path;
        ctx.Request.PathBase = pathBase;
        ctx.Request.QueryString = new QueryString(query);
        return ctx;
    }
}
