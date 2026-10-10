using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using ShellDocs.Components.Content;
using ShellDocs.Templates;
using Xunit;

namespace ShellDocs.Tests;

public class NotFoundTests : IDisposable
{
    private readonly string _root;

    public NotFoundTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-notfound-" + Guid.NewGuid().ToString("N"));
        var page = Path.Combine(_root, "docs", "getting-started.md");
        Directory.CreateDirectory(Path.GetDirectoryName(page)!);
        File.WriteAllText(page, "---\ntitle: Getting started\n---\n# Getting started\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private ComponentRenderHarness Harness() => new(o => o.ContentRoot = _root, "http://localhost/docs/nope");

    // Cascades an HttpContext the way static SSR / prerendering does.
    public class WithHttpContext : ComponentBase
    {
        [Parameter] public HttpContext? Context { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenComponent<CascadingValue<HttpContext>>(0);
            b.AddComponentParameter(1, "Value", Context);
            b.AddComponentParameter(2, "ChildContent", (RenderFragment)(c =>
            {
                c.OpenComponent<DocsNotFound>(0);
                c.CloseComponent();
            }));
            b.CloseComponent();
        }
    }

    // Runs OnStarting callbacks the way Kestrel does when the headers go out.
    private sealed class StartingFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _callbacks = new();
        public override void OnStarting(Func<object, Task> callback, object state) => _callbacks.Add((callback, state));
        public async Task StartAsync() { foreach (var (c, s) in _callbacks) await c(s); }
    }

    [Fact]
    public async Task DocsNotFound_Sets404_AsTheHeadersGoOut_AndLinksBackToTheDocs()
    {
        var http = new DefaultHttpContext();
        var feature = new StartingFeature();
        http.Features.Set<IHttpResponseFeature>(feature);
        var html = await Harness().RenderAsync<WithHttpContext>(new() { ["Context"] = http });

        Assert.Equal(StatusCodes.Status200OK, http.Response.StatusCode); // Blazor still renders the body
        await feature.StartAsync();
        Assert.Equal(StatusCodes.Status404NotFound, http.Response.StatusCode);
        Assert.Contains("data-shelldocs-not-found", html);
        Assert.Matches("<code[^>]*>/docs/nope</code>", html);
        Assert.Matches("<a href=\"/docs/getting-started\"[^>]*>", html);
    }

    [Fact]
    public async Task DocsNotFound_WithoutAnHttpContext_StillRenders()
    {
        var html = await Harness().RenderAsync<DocsNotFound>(new() { ["Path"] = "/somewhere", ["HomeHref"] = "/start", ["HomeLabel"] = "Start" });

        Assert.Matches("<code[^>]*>/somewhere</code>", html);
        Assert.Matches("<a href=\"/start\"[^>]*>.*Start</a>", html);
    }

    [Fact]
    public void InitTemplate_UsesDocsNotFound()
    {
        Assert.Contains("<DocsNotFound />", ScaffoldTemplates.DocsPageRazor);
        Assert.DoesNotContain("doc-not-found", ScaffoldTemplates.DocsPageRazor);
    }

    [Fact]
    public void Build_TreatsTheNotFoundView_AsAFailure()
    {
        var cli = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "shelldocs") ?? Assembly.Load("shelldocs");
        var isNotFound = cli.GetType("ShellDocs.CLI.Commands.PrerenderRunner", throwOnError: true)!
            .GetMethod("IsNotFoundPage", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;

        Assert.True((bool)isNotFound.Invoke(null, ["<div class=\"docs-not-found\" data-shelldocs-not-found>"])!);
        Assert.False((bool)isNotFound.Invoke(null, ["<main>Getting started</main>"])!);
    }
}
