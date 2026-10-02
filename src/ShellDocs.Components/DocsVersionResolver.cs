using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using ShellDocs.Core;

namespace ShellDocs.Components;

/* Every version question the chrome asks. Stateless — callers pass the current
   path — so the answers are fixed at prerender time and work on static hosts. */
public sealed class DocsVersionResolver
{
    private readonly ShellDocsOptions _options;
    private readonly NavigationGraph _graph;
    private readonly ConcurrentDictionary<string, NavigationNode?> _nodes = new(StringComparer.Ordinal);

    public DocsVersionResolver(ShellDocsOptions options, NavigationGraph graph)
    {
        _options = options;
        _graph = graph;
    }

    public IReadOnlyList<DocsVersion> Versions => _options.Versions;
    public bool HasVersions => _options.Versions.Count > 0;

    // Base-relative path, without query or fragment.
    public static string PathOf(NavigationManager nav)
        => UrlPath.Normalize(nav.ToBaseRelativePath(nav.Uri));

    // Longest matching RootUrl wins.
    public DocsVersion? FindContaining(string? path)
    {
        DocsVersion? best = null;
        foreach (var v in _options.Versions)
        {
            if (!UrlPath.IsUnder(path, v.RootUrl)) continue;
            if (best is null || UrlPath.Normalize(v.RootUrl).Length > UrlPath.Normalize(best.RootUrl).Length) best = v;
        }
        return best;
    }

    // Containing version → latest → first configured → null (no versions).
    public DocsVersion? Current(string? path)
        => FindContaining(path)
           ?? _options.Versions.FirstOrDefault(v => v.IsLatest)
           ?? _options.Versions.FirstOrDefault();

    // Prefers the folder whose on-disk path maps to RootUrl; otherwise the outermost
    // section whose pages all live under it (graphs built without paths).
    public NavigationNode? GetVersionNode(DocsVersion version)
        => _nodes.GetOrAdd(version.Id, _ => _graph.FindFolder(version.RootUrl) ?? FindByPrefix(_graph.Root, version.RootUrl));

    private static NavigationNode? FindByPrefix(NavigationNode node, string root)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind != NodeKind.Section) continue;
            var pages = Pages(child).ToList();
            if (pages.Count > 0 && pages.All(p => UrlPath.IsUnder(p.Url, root))) return child;
            var nested = FindByPrefix(child, root);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static IEnumerable<NavigationNode> Pages(NavigationNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.Page && !string.IsNullOrEmpty(child.Url)) yield return child;
            foreach (var p in Pages(child)) yield return p;
        }
    }

    public IReadOnlyList<NavigationNode> GetSidebarNodes(string? path)
    {
        var version = FindContaining(path);
        var node = version is null ? null : GetVersionNode(version);
        return node?.Children ?? _graph.Root.Children;
    }

    public bool InSameScope(string? urlA, string? urlB)
        => FindContaining(urlA)?.Id == FindContaining(urlB)?.Id;

    // Visible: entries in the current version, plus anything outside every version root.
    public bool IsVisibleFrom(string? url, string? currentPath)
    {
        if (!HasVersions) return true;
        var owner = FindContaining(url);
        return owner is null || owner.Id == Current(currentPath)?.Id;
    }

    public IEnumerable<SearchEntry> FilterSearch(IEnumerable<SearchEntry> entries, string? currentPath)
        => HasVersions ? entries.Where(e => IsVisibleFrom(e.Url, currentPath)) : entries;

    public bool IsVersionNode(NavigationNode node)
    {
        foreach (var v in _options.Versions)
        {
            if (ReferenceEquals(GetVersionNode(v), node)) return true;
        }
        return false;
    }

    public string ResolvePackageRoot(DocsPackage package, DocsVersion? version)
        => version is not null && package.IsVersioned
            ? package.RootUrl.Replace(DocsPackage.VersionToken, version.Id, StringComparison.Ordinal)
            : package.RootUrl;

    public string ResolvePackageRoot(DocsPackage package, string? currentPath)
        => ResolvePackageRoot(package, Current(currentPath));

    // Longest resolved RootUrl prefix wins; Packages[0] otherwise.
    public DocsPackage? SelectPackage(string? currentPath)
    {
        if (_options.Packages.Count == 0) return null;
        var version = Current(currentPath);
        DocsPackage? best = null;
        var bestLen = -1;
        foreach (var pkg in _options.Packages)
        {
            var root = UrlPath.Normalize(ResolvePackageRoot(pkg, version));
            if (!UrlPath.IsUnder(currentPath, root) || root.Length <= bestLen) continue;
            best = pkg;
            bestLen = root.Length;
        }
        return best ?? _options.Packages[0];
    }

    // Unversioned roots stay verbatim (may be a custom page outside the graph);
    // versioned roots that aren't a page link to their first page instead of a 404.
    public string GetPackageHref(DocsPackage package, string? currentPath)
        => GetPackageHref(package, Current(currentPath));

    public string GetPackageHref(DocsPackage package, DocsVersion? version)
    {
        var root = ResolvePackageRoot(package, version);
        if (!package.IsVersioned || _graph.ResolveByUrl(root) is not null) return root;
        return _graph.FirstPageUnder(root)?.Url ?? root;
    }

    // Same relative page in the target version → current package's root there →
    // the target version's first page → its RootUrl.
    public string GetVersionHref(string? currentPath, DocsVersion target)
    {
        var from = FindContaining(currentPath);
        if (from is not null)
        {
            var rel = UrlPath.RelativeTo(currentPath, from.RootUrl);
            var same = UrlPath.Combine(target.RootUrl, rel);
            var node = _graph.ResolveByUrl(same);
            if (node is not null) return node.Url;
        }

        var pkg = SelectPackage(currentPath);
        if (pkg is not null && pkg.IsVersioned)
        {
            var pkgRoot = ResolvePackageRoot(pkg, target);
            if (UrlPath.IsUnder(currentPath, ResolvePackageRoot(pkg, Current(currentPath))))
            {
                var hit = _graph.ResolveByUrl(pkgRoot) ?? _graph.FirstPageUnder(pkgRoot);
                if (hit is not null) return hit.Url;
            }
        }

        var first = GetVersionNode(target) is { } vnode ? FirstPage(vnode) : null;
        first ??= _graph.FirstPageUnder(target.RootUrl);
        return first?.Url ?? UrlPath.Normalize(target.RootUrl);
    }

    private static NavigationNode? FirstPage(NavigationNode node)
        => Pages(node).FirstOrDefault();
}
