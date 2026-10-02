namespace ShellDocs.Core;

public class NavigationGraph
{
    public NavigationNode Root { get; }

    private readonly Dictionary<string, NavigationNode> _byUrl;
    private readonly List<NavigationNode> _flatPages;
    private readonly List<NavigationNode> _hiddenRoots = new();

    public NavigationGraph(NavigationNode root, IEnumerable<NavigationNode>? hiddenPages = null)
    {
        Root = root;
        _byUrl = new Dictionary<string, NavigationNode>(StringComparer.OrdinalIgnoreCase);
        _flatPages = new List<NavigationNode>();
        Index(root);
        // Hidden pages resolve by URL but stay out of _flatPages, so prev/next skips them.
        if (hiddenPages is not null)
        {
            foreach (var page in hiddenPages)
            {
                _hiddenRoots.Add(page);
                IndexHidden(page);
            }
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

    // Nearest neighbours `inScope` accepts, skipping the rest (keeps prev/next inside a version).
    public (NavigationNode? Prev, NavigationNode? Next) GetPrevNext(NavigationNode node, Func<NavigationNode, bool> inScope)
    {
        var i = _flatPages.IndexOf(node);
        if (i < 0) return (null, null);
        NavigationNode? prev = null, next = null;
        for (var j = i - 1; j >= 0; j--)
        {
            if (inScope(_flatPages[j])) { prev = _flatPages[j]; break; }
        }
        for (var j = i + 1; j < _flatPages.Count; j++)
        {
            if (inScope(_flatPages[j])) { next = _flatPages[j]; break; }
        }
        return (prev, next);
    }

    // Section node for the content folder at `folderUrl`, searching hidden folders too.
    // Null for graphs built without on-disk paths.
    public NavigationNode? FindFolder(string folderUrl)
    {
        if (string.IsNullOrEmpty(Root.Path)) return null;
        var target = UrlPath.Normalize(folderUrl);
        foreach (var n in FlattenFrom(Root).Concat(_hiddenRoots.SelectMany(FlattenFrom)))
        {
            if (n.Kind != NodeKind.Section || n == Root || string.IsNullOrEmpty(n.Path)) continue;
            var rel = System.IO.Path.GetRelativePath(Root.Path, n.Path).Replace('\\', '/');
            if (rel.StartsWith("..", StringComparison.Ordinal)) continue;
            if (string.Equals(UrlPath.Normalize(rel), target, StringComparison.OrdinalIgnoreCase)) return n;
        }
        return null;
    }

    // First visible page (tree order) whose URL is `urlPrefix` or below it.
    public NavigationNode? FirstPageUnder(string urlPrefix)
        => _flatPages.FirstOrDefault(p => UrlPath.IsUnder(p.Url, urlPrefix));

    public NavigationNode? FirstPageUnder(string urlPrefix, Func<NavigationNode, bool> predicate)
        => _flatPages.FirstOrDefault(p => UrlPath.IsUnder(p.Url, urlPrefix) && predicate(p));

    // URL of every visible content folder below the root. Empty for graphs built without on-disk paths.
    public IEnumerable<(string Url, NavigationNode Node)> Folders()
    {
        if (string.IsNullOrEmpty(Root.Path)) yield break;
        foreach (var n in FlattenFrom(Root))
        {
            if (n.Kind != NodeKind.Section || n == Root || string.IsNullOrEmpty(n.Path)) continue;
            var rel = System.IO.Path.GetRelativePath(Root.Path, n.Path).Replace('\\', '/');
            if (rel.StartsWith("..", StringComparison.Ordinal)) continue;
            yield return (UrlPath.Normalize(rel), n);
        }
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

    // Visible + hidden pages; static prerender needs both.
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

    private static string Normalize(string url) => UrlPath.Normalize(url);
}
