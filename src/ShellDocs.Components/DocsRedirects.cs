using ShellDocs.Core;

namespace ShellDocs.Components;

public record DocsRedirectTarget(string Url, bool Permanent);

/* Where a URL that isn't a page should go. Computed from the graph and versions:
   content folders and version roots → their first page (a folder that holds version
   folders → its first unversioned page, else the latest version's), and unversioned
   URLs that exist in the latest version → that page. AddRedirect rules apply on top
   and may chain into the computed ones. Existing pages are never redirected except
   by an explicit rule. */
public sealed class DocsRedirects
{
    private const int MaxHops = 8;

    private readonly ShellDocsOptions _options;
    private readonly NavigationGraph _graph;
    private readonly DocsVersionResolver _versions;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _computed;

    public DocsRedirects(ShellDocsOptions options, NavigationGraph graph, DocsVersionResolver versions)
    {
        _options = options;
        _graph = graph;
        _versions = versions;
        _computed = new Lazy<IReadOnlyDictionary<string, string>>(Compute);
    }

    public DocsRedirectTarget? Resolve(string? path)
    {
        var start = UrlPath.Normalize(path);
        var current = start;
        var permanent = true;
        for (var hop = 0; hop < MaxHops; hop++)
        {
            string? next;
            if (ApplyRule(current) is { } rule)
            {
                next = rule.Url;
                permanent &= rule.Permanent;
            }
            else if (_graph.ResolveByUrl(current) is null && _computed.Value.TryGetValue(current, out var target))
            {
                next = target;
                permanent = false;
            }
            else break;

            if (string.Equals(next, current, StringComparison.OrdinalIgnoreCase)) break;
            current = next;
        }
        return string.Equals(current, start, StringComparison.OrdinalIgnoreCase) ? null : new DocsRedirectTarget(current, permanent);
    }

    // Every redirecting URL that can be listed up front, for static output.
    public IReadOnlyList<(string From, string To)> All()
    {
        var sources = new HashSet<string>(_computed.Value.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var rule in _options.Redirects)
        {
            sources.Add(UrlPath.Normalize(rule.From));
            foreach (var page in _graph.Flatten().Where(n => n.Kind == NodeKind.Page && UrlPath.IsUnder(n.Url, rule.To)))
                sources.Add(UrlPath.Combine(rule.From, UrlPath.RelativeTo(page.Url, rule.To)));
        }
        return sources
            .Select(s => (From: s, Target: Resolve(s)))
            .Where(x => x.Target is not null)
            .Select(x => (x.From, x.Target!.Url))
            .OrderBy(x => x.From, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private DocsRedirectTarget? ApplyRule(string path)
    {
        foreach (var rule in _options.Redirects)
        {
            // Already inside the target: "/docs" → "/docs/v2" must not re-match /docs/v2/x.
            if (!UrlPath.IsUnder(path, rule.From) || UrlPath.IsUnder(path, rule.To)) continue;
            return new DocsRedirectTarget(UrlPath.Combine(rule.To, UrlPath.RelativeTo(path, rule.From)), rule.Permanent);
        }
        return null;
    }

    private IReadOnlyDictionary<string, string> Compute()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string from, string? to)
        {
            from = UrlPath.Normalize(from);
            if (to is null || from == "/" || map.ContainsKey(from) || _graph.ResolveByUrl(from) is not null) return;
            to = UrlPath.Normalize(to);
            if (!string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) map[from] = to;
        }

        foreach (var version in _versions.Versions)
            Add(version.RootUrl, _versions.GetVersionHref(null, version));

        var folders = _graph.Folders().ToList();
        foreach (var (url, _) in folders)
            Add(url, FolderTarget(url));

        var latest = _versions.Versions.FirstOrDefault(v => v.IsLatest);
        if (latest is not null)
        {
            var parent = Parent(latest.RootUrl);
            foreach (var page in _graph.Flatten().Where(n => n.Kind == NodeKind.Page && UrlPath.IsUnder(n.Url, latest.RootUrl)))
                AddLegacy(page.Url, page.Url);
            foreach (var (url, _) in folders.Where(f => UrlPath.IsUnder(f.Url, latest.RootUrl)))
                AddLegacy(url, FolderTarget(url));

            void AddLegacy(string versionedUrl, string? target)
            {
                var legacy = UrlPath.Combine(parent, UrlPath.RelativeTo(versionedUrl, latest.RootUrl));
                if (_versions.FindContaining(legacy) is null) Add(legacy, target);
            }
        }
        return map;
    }

    // First page in the folder within the folder's own version scope. A folder outside
    // every version (e.g. /docs holding v1/ and v2/) prefers its unversioned pages, as
    // the sidebar does, then falls back to the latest version.
    private string? FolderTarget(string folderUrl)
    {
        var scope = _versions.FindContaining(folderUrl);
        var page = _graph.FirstPageUnder(folderUrl, p => _versions.FindContaining(p.Url)?.Id == scope?.Id);
        if (page is not null) return page.Url;
        if (scope is null && _versions.Current(folderUrl) is { } fallback && _versions.Versions.Any(v => UrlPath.IsUnder(v.RootUrl, folderUrl)))
            return _versions.GetVersionHref(null, fallback);
        return null;
    }

    private static string Parent(string url)
    {
        var u = UrlPath.Normalize(url);
        var slash = u.LastIndexOf('/');
        return slash <= 0 ? "/" : u[..slash];
    }
}
