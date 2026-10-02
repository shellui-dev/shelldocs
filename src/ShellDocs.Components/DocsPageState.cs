using Microsoft.AspNetCore.Components;
using ShellDocs.Core;
using ShellDocs.Markdown;

namespace ShellDocs.Components;

// Populated by MarkdownContent on each page render, read by DocsLayout's chrome
// (TOC, PrevNext, Breadcrumb) so those components don't need per-page wiring.
public sealed class DocsPageState : IDisposable
{
    private readonly NavigationGraph _graph;
    private readonly NavigationManager _nav;
    private readonly DocsVersionResolver? _versions;

    public DocsPageState(NavigationGraph graph, NavigationManager nav)
        : this(graph, nav, null) { }

    // With a resolver, prev/next stay in the page's version and the version folder leaves the breadcrumb.
    public DocsPageState(NavigationGraph graph, NavigationManager nav, DocsVersionResolver? versions)
    {
        _graph = graph;
        _nav = nav;
        _versions = versions;
        _nav.LocationChanged += OnLocationChanged;
    }

    public RenderedDocument? Document { get; private set; }
    public NavigationNode? CurrentNode { get; private set; }
    public NavigationNode? Prev { get; private set; }
    public NavigationNode? Next { get; private set; }
    public IReadOnlyList<NavigationNode> Breadcrumbs { get; private set; } = Array.Empty<NavigationNode>();

    public event Action? OnChange;

    public void SetDocument(RenderedDocument? document)
    {
        Document = document;
        Recompute();
    }

    private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
        => Recompute();

    private void Recompute()
    {
        var path = new Uri(_nav.Uri).AbsolutePath;
        CurrentNode = _graph.ResolveByUrl(path);
        if (CurrentNode is null)
        {
            Prev = Next = null;
            Breadcrumbs = Array.Empty<NavigationNode>();
        }
        else if (_versions is { HasVersions: true } versions)
        {
            var current = CurrentNode;
            (Prev, Next) = _graph.GetPrevNext(current, n => versions.InSameScope(n.Url, current.Url));
            Breadcrumbs = _graph.GetBreadcrumb(current).Where(n => !versions.IsVersionNode(n)).ToList();
        }
        else
        {
            (Prev, Next) = _graph.GetPrevNext(CurrentNode);
            Breadcrumbs = _graph.GetBreadcrumb(CurrentNode);
        }
        OnChange?.Invoke();
    }

    public void Dispose() => _nav.LocationChanged -= OnLocationChanged;
}
