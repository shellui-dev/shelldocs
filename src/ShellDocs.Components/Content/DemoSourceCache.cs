using System.Collections.Concurrent;

namespace ShellDocs.Components.Content;

/* Shallowest {root}/.../X.razor wins (then ordinal). Content is re-read only
   when the file's write time changes; misses aren't cached, so new files appear. */
internal static class DemoSourceCache
{
    private sealed record Entry(string Path, DateTime LastWriteUtc, string Source);

    private static readonly ConcurrentDictionary<(string Root, string Component), Entry> _cache = new();

    public static string? FindPath(string root, string component)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;
        var fileName = component + ".razor";
        IEnumerable<string> matches;
        try { matches = Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }

        return matches
            // EnumerateFiles' pattern is case-insensitive on Windows only.
            .Where(p => string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar))
            .ThenBy(p => p, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static string? Get(string root, string component)
    {
        var key = (Path.GetFullPath(root), component);
        if (_cache.TryGetValue(key, out var hit) && File.Exists(hit.Path)
            && File.GetLastWriteTimeUtc(hit.Path) == hit.LastWriteUtc)
            return hit.Source;

        var path = FindPath(key.Item1, component);
        if (path is null)
        {
            _cache.TryRemove(key, out _);
            return null;
        }

        try
        {
            var entry = new Entry(path, File.GetLastWriteTimeUtc(path), File.ReadAllText(path));
            _cache[key] = entry;
            return entry.Source;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
