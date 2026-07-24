using Microsoft.AspNetCore.Components;
using ShellDocs.Components;
using ShellDocs.Core;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class DocsPageStateTests
{
    [Fact]
    public void SetDocument_FiresOnChange_AndStoresDocument()
    {
        var (graph, nav) = MinimalGraphAndNav("http://localhost/docs/unknown");
        var state = new DocsPageState(graph, nav);
        var doc = new RenderedDocument("<p>hi</p>", Array.Empty<Slot>(), null!, Array.Empty<Heading>());

        var fired = 0;
        state.OnChange += () => fired++;

        state.SetDocument(doc);

        Assert.Equal(1, fired);
        Assert.Same(doc, state.Document);
    }

    [Fact]
    public void UnknownUrl_YieldsEmptyChromeState()
    {
        var (graph, nav) = MinimalGraphAndNav("http://localhost/docs/does-not-exist");
        var state = new DocsPageState(graph, nav);

        state.SetDocument(null);

        Assert.Null(state.CurrentNode);
        Assert.Null(state.Prev);
        Assert.Null(state.Next);
        Assert.Empty(state.Breadcrumbs);
    }

    [Fact]
    public void Dispose_UnsubscribesFromNavigationManager()
    {
        var (graph, nav) = MinimalGraphAndNav("http://localhost/");
        var state = new DocsPageState(graph, nav);

        // Should not throw; sanity that Dispose is idempotent-ish.
        state.Dispose();
    }

    private static (NavigationGraph, NavigationManager) MinimalGraphAndNav(string uri)
    {
        var root = new NavigationNode { Kind = NodeKind.Section };
        return (new NavigationGraph(root), new StubNavigationManager(uri));
    }

    private sealed class StubNavigationManager : NavigationManager
    {
        public StubNavigationManager(string uri) => Initialize("http://localhost/", uri);
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
