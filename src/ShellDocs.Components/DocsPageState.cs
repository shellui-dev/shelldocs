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

    public DocsPageState(NavigationGraph graph, NavigationManager nav)
    {
        _graph = graph;
        _nav = nav;
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
        else
        {
            (Prev, Next) = _graph.GetPrevNext(CurrentNode);
            Breadcrumbs = _graph.GetBreadcrumb(CurrentNode);
        }
        OnChange?.Invoke();
    }

    public void Dispose() => _nav.LocationChanged -= OnLocationChanged;
}
