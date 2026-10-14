using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShellDocs.Components;
using ShellDocs.Components.Chrome;
using ShellDocs.Components.Layouts;
using ShellDocs.Core;
using Xunit;

namespace ShellDocs.Tests;

// Search runs in shelldocs.js against /_shelldocs/search.json, so it needs no Blazor runtime.
public class StaticSearchTests : IDisposable
{
    private readonly string _root;

    public StaticSearchTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-search-" + Guid.NewGuid().ToString("N"));
        Write("docs/introduction.md", "---\ntitle: Introduction\ndescription: Start here.\n---\n# Introduction\n\nWelcome to the docs.\n\n## Install\n\nRun the tool.\n");
        Write("docs/v2/button.md", "---\ntitle: Button\n---\n# Button\n\nA café button.\n");
        Write("docs/v1/button.md", "---\ntitle: Button\n---\n# Button\n\nThe old button.\n");
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

    private ComponentRenderHarness Harness(DocsLayoutVariant layout = DocsLayoutVariant.Sidebar, bool versions = true) => new(o =>
    {
        o.ContentRoot = _root;
        o.LayoutVariant = layout;
        if (!versions) return;
        o.AddVersion("v2", "v2.0", "/docs/v2", latest: true);
        o.AddVersion("v1", "v1.0", "/docs/v1");
    }, "http://localhost/docs/introduction");

    [Fact]
    public async Task SearchDialog_IsAHiddenTemplate_WithNoBlazorHandlers()
    {
        var html = await Harness().RenderAsync<SearchDialog>();

        Assert.Matches("<div class=\"search-backdrop\"[^>]*data-search-dialog[^>]*hidden", html);
        Assert.Contains("data-search-input", html);
        Assert.Contains("data-search-results", html);
        Assert.Matches("<a class=\"search-result\"[^>]*data-search-row=\"p\"", html);
        Assert.Matches("<a class=\"search-result\"[^>]*data-search-row=\"h\"", html);
        Assert.Matches("<div class=\"search-empty\"[^>]*data-search-row=\"empty\"", html);
        Assert.DoesNotContain("blazor:on", html);
    }

    [Fact]
    public async Task SearchButtons_OpenSearchThroughShelldocsJs()
    {
        var sidebarLayout = await Harness().RenderAsync<DocsLayout>();
        var topNavLayout = await Harness(DocsLayoutVariant.TopNav).RenderAsync<DocsLayout>();

        foreach (var (html, css) in new[]
        {
            (sidebarLayout, "docs-collapsed-tool\"[^>]*aria-label=\"Search\""),
            (sidebarLayout, "docs-sidebar-search"),
            (sidebarLayout, "docs-mobile-tool\"[^>]*aria-label=\"Search\""),
            (topNavLayout, "docs-header-search"),
        })
        {
            var button = Regex.Match(html, "<button[^>]*" + css + "[^>]*>").Value;
            Assert.Contains("data-search-open", button);
            Assert.DoesNotContain("blazor:onclick", button);
        }
    }

    [Fact]
    public void IndexFile_ListsEntries_WithTheVersionThatOwnsThem()
    {
        var services = Harness().Services;
        var bytes = DocsSearchIndexFile.Build(services.GetRequiredService<SearchIndex>(), services.GetRequiredService<DocsVersionResolver>());
        using var json = JsonDocument.Parse(bytes);

        var versions = json.RootElement.GetProperty("versions").EnumerateArray().ToList();
        Assert.Equal(["v2", "v1"], versions.Select(v => v.GetProperty("id").GetString()));
        Assert.Equal("/docs/v2", versions[0].GetProperty("root").GetString());
        Assert.True(versions[0].GetProperty("latest").GetBoolean());
        Assert.False(versions[1].TryGetProperty("latest", out _));

        var entries = json.RootElement.GetProperty("entries").EnumerateArray().ToList();
        var intro = entries.Single(e => e.GetProperty("u").GetString() == "/docs/introduction");
        Assert.Equal("Introduction", intro.GetProperty("t").GetString());
        Assert.Equal("Start here.", intro.GetProperty("d").GetString());
        Assert.Equal("p", intro.GetProperty("k").GetString());
        Assert.Contains("Welcome to the docs.", intro.GetProperty("b").GetString());
        Assert.False(intro.TryGetProperty("v", out _));

        var heading = entries.Single(e => e.GetProperty("u").GetString() == "/docs/introduction#install");
        Assert.Equal("h", heading.GetProperty("k").GetString());
        Assert.Equal("Introduction", heading.GetProperty("s").GetString());
        Assert.False(heading.TryGetProperty("b", out _));

        Assert.Equal("v2", entries.Single(e => e.GetProperty("u").GetString() == "/docs/v2/button").GetProperty("v").GetString());
        Assert.Equal("v1", entries.Single(e => e.GetProperty("u").GetString() == "/docs/v1/button").GetProperty("v").GetString());
        // Written as UTF-8, not \u escapes.
        Assert.Contains("café", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void IndexFile_WithoutVersions_HasNoOwners()
    {
        var services = Harness(versions: false).Services;
        var bytes = DocsSearchIndexFile.Build(services.GetRequiredService<SearchIndex>(), services.GetRequiredService<DocsVersionResolver>());
        using var json = JsonDocument.Parse(bytes);

        Assert.Empty(json.RootElement.GetProperty("versions").EnumerateArray());
        Assert.All(json.RootElement.GetProperty("entries").EnumerateArray(), e => Assert.False(e.TryGetProperty("v", out _)));
    }

    [Fact]
    public async Task Middleware_ServesTheIndex_AndRevalidates()
    {
        var file = Harness().Services.GetRequiredService<DocsSearchIndexFile>();
        var passedOn = 0;
        var middleware = new DocsSearchIndexMiddleware(_ => { passedOn++; return Task.CompletedTask; }, file);

        var hit = Context(DocsSearchIndexMiddleware.IndexPath);
        hit.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(hit);
        Assert.Equal(0, passedOn);
        Assert.Equal("application/json; charset=utf-8", hit.Response.ContentType);
        Assert.Equal(file.Bytes, ((MemoryStream)hit.Response.Body).ToArray());
        Assert.Equal(file.ETag, hit.Response.Headers.ETag.ToString());

        var cached = Context(DocsSearchIndexMiddleware.IndexPath);
        cached.Request.Headers.IfNoneMatch = file.ETag;
        await middleware.InvokeAsync(cached);
        Assert.Equal(StatusCodes.Status304NotModified, cached.Response.StatusCode);

        await middleware.InvokeAsync(Context("/docs/introduction"));
        var post = Context(DocsSearchIndexMiddleware.IndexPath);
        post.Request.Method = "POST";
        await middleware.InvokeAsync(post);
        Assert.Equal(2, passedOn);
    }

    [Fact]
    public void AddShellDocs_PutsTheIndexMiddlewareInThePipeline()
    {
        var filters = Harness().Services.GetServices<Microsoft.AspNetCore.Hosting.IStartupFilter>();
        Assert.Contains(filters, f => f is DocsSearchStartupFilter);
    }

    [Fact]
    public async Task HomeLayout_HasTheSearchDialog()
    {
        var html = await Harness().RenderAsync<HomeLayout>();
        Assert.Contains("data-search-dialog", html);
    }

    [Fact]
    public void ShelldocsJs_SearchesTheIndexInTheBrowser()
    {
        var js = File.ReadAllText(FindAsset("shelldocs.js"));
        Assert.Contains("'_shelldocs/search.json'", js);
        Assert.Contains("[data-search-dialog]", js);
        Assert.Contains("[data-search-open]", js);
        Assert.DoesNotContain("invokeMethodAsync('OpenFromJs')", js);
    }

    [Fact]
    public void Build_CopiesTheIndexIntoTheExport()
    {
        var cli = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "shelldocs") ?? Assembly.Load("shelldocs");
        var write = cli.GetType("ShellDocs.CLI.Commands.PrerenderRunner", throwOnError: true)!
            .GetMethod("WriteSearchIndex", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        var output = Path.Combine(_root, "out");
        Directory.CreateDirectory(output);

        using var served = new HttpClient(new StubHandler(HttpStatusCode.OK, "{\"versions\":[],\"entries\":[]}")) { BaseAddress = new Uri("http://localhost") };
        var written = (long)write.Invoke(null, [served, output])!;
        var path = Path.Combine(output, "_shelldocs", "search.json");
        Assert.Equal(new FileInfo(path).Length, written);
        Assert.Equal("{\"versions\":[],\"entries\":[]}", File.ReadAllText(path));

        // A site on a ShellDocs.Components without the endpoint.
        var older = Path.Combine(_root, "older");
        Directory.CreateDirectory(older);
        using var missing = new HttpClient(new StubHandler(HttpStatusCode.NotFound, "")) { BaseAddress = new Uri("http://localhost") };
        Assert.Equal(0L, (long)write.Invoke(null, [missing, older])!);
        Assert.False(Directory.Exists(Path.Combine(older, "_shelldocs")));
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/_shelldocs/search.json", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => SendAsync(request, cancellationToken).GetAwaiter().GetResult();
    }

    private static DefaultHttpContext Context(string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = path;
        return ctx;
    }

    private static string FindAsset(string name)
    {
        var testDir = Path.GetDirectoryName(typeof(StaticSearchTests).Assembly.Location)!;
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
