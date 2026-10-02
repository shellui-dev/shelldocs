namespace ShellDocs.Core;

// App-relative URL path helpers. Dots are ordinary characters, so "v0.2.1" stays a folder segment.
public static class UrlPath
{
    // Leading slash, no trailing slash (except "/" itself), query/fragment stripped.
    public static string Normalize(string? url)
    {
        var s = (url ?? "").Trim();
        var cut = s.IndexOfAny(['?', '#']);
        if (cut >= 0) s = s[..cut];
        if (!s.StartsWith('/')) s = "/" + s;
        while (s.Length > 1 && s.EndsWith('/')) s = s[..^1];
        return s;
    }

    // Segment-aware: "/docs/v0.30/x" is not under "/docs/v0.3". Case-insensitive,
    // matching NavigationGraph.ResolveByUrl.
    public static bool IsUnder(string? path, string? root)
    {
        var p = Normalize(path);
        var r = Normalize(root);
        if (r == "/") return true;
        if (!p.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return false;
        return p.Length == r.Length || p[r.Length] == '/';
    }

    // "/components/button" for a page below root, "" for root itself, null when outside.
    public static string? RelativeTo(string? path, string? root)
    {
        if (!IsUnder(path, root)) return null;
        var p = Normalize(path);
        var r = Normalize(root);
        return r == "/" ? (p == "/" ? "" : p) : p[r.Length..];
    }

    public static string Combine(string root, string? relative)
    {
        var r = Normalize(root);
        if (string.IsNullOrEmpty(relative) || relative == "/") return r;
        var rel = relative.StartsWith('/') ? relative : "/" + relative;
        return Normalize(r == "/" ? rel : r + rel);
    }
}
