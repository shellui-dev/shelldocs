namespace ShellDocs.Core;

public class NavigationGraph
{
    public NavigationNode Root { get; }

    private readonly Dictionary<string, NavigationNode> _byUrl;
    private readonly List<NavigationNode> _flatPages;

    public NavigationGraph(NavigationNode root, IEnumerable<NavigationNode>? hiddenPages = null)
    {
        Root = root;
        _byUrl = new Dictionary<string, NavigationNode>(StringComparer.OrdinalIgnoreCase);
        _flatPages = new List<NavigationNode>();
        Index(root);
        // Hidden pages route (URLs resolve) but never appear in the visible
        // tree, so they're excluded from _flatPages (prev/next skips them).
        if (hiddenPages is not null)
        {
            foreach (var page in hiddenPages) IndexHidden(page);
        }
    }

    private void IndexHidden(NavigationNode node)
    {
        if (node.Kind == NodeKind.Page && !string.IsNullOrEmpty(node.Url))
        {
            _byUrl[Normalize(node.Url)] = node;
        }
        foreach (var child in node.Children) IndexHidden(child);
    }

    public NavigationNode? ResolveByUrl(string url)
    {
        var key = Normalize(url);
        return _byUrl.TryGetValue(key, out var node) ? node : null;
    }

    public (NavigationNode? Prev, NavigationNode? Next) GetPrevNext(NavigationNode node)
    {
        var i = _flatPages.IndexOf(node);
        if (i < 0) return (null, null);
        var prev = i > 0 ? _flatPages[i - 1] : null;
        var next = i < _flatPages.Count - 1 ? _flatPages[i + 1] : null;
        return (prev, next);
    }

    public IReadOnlyList<NavigationNode> GetBreadcrumb(NavigationNode node)
    {
        var chain = new List<NavigationNode>();
        var current = node;
        while (current is not null && current != Root)
        {
            chain.Add(current);
            current = current.Parent;
        }
        chain.Reverse();
        return chain;
    }

    public IEnumerable<NavigationNode> Flatten()
    {
        return FlattenFrom(Root);
    }

    // Visible + hidden pages. Static prerender needs both — hidden pages
    // still route at runtime.
    public IEnumerable<string> AllUrls => _byUrl.Keys;

    private static IEnumerable<NavigationNode> FlattenFrom(NavigationNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var n in FlattenFrom(child)) yield return n;
        }
    }

    private void Index(NavigationNode node)
    {
        if (node.Kind == NodeKind.Page && !string.IsNullOrEmpty(node.Url))
        {
            _byUrl[Normalize(node.Url)] = node;
            _flatPages.Add(node);
        }
        foreach (var child in node.Children) Index(child);
    }

    private static string Normalize(string url)
    {
        var s = url.Trim();
        if (!s.StartsWith('/')) s = "/" + s;
        if (s.Length > 1 && s.EndsWith('/')) s = s[..^1];
        return s;
    }
}
