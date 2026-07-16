namespace ShellDocs.Components.Chrome;

internal static class SidebarIcons
{
    /* Lightweight title→lucide-svg map — matches fumadocs' inline icon feel
       without forcing content authors to configure per-page icons. */
    private static readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Getting Started"] = "M13 2 3 14h9l-1 8 10-12h-9l1-8z",
        ["Introduction"]    = "M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10zM12 8v4l3 3",
        ["Installation"]    = "M12 2v10m0 0-4-4m4 4 4-4M4 15v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3",
        ["Quickstart"]      = "M13 2 3 14h9l-1 8 10-12h-9l1-8z",
        ["Markdown"]        = "M4 4h16v16H4z M4 9h16 M9 4v16",
        ["Frontmatter"]     = "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M9 13h6 M9 17h6",
        ["Components"]      = "M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z",
        ["Callout"]         = "M12 8v4 M12 16h.01 M22 12A10 10 0 1 1 12 2a10 10 0 0 1 10 10z",
        ["Card"]            = "M3 5h18v14H3z M3 10h18",
        ["Tabs"]            = "M3 3h18v6H3z M3 13h8v8H3z M13 13h8v8h-8z",
        ["Code Group"]      = "m8 6-6 6 6 6 M16 6l6 6-6 6",
        ["Theming"]         = "M2 12a10 10 0 1 0 10 10c0-3-3-3-3-6a4 4 0 0 1 4-4h3a6 6 0 0 0 6-6 10 10 0 0 0-20 6z",
        ["Palette"]         = "M12 22a10 10 0 1 1 10-10 5 5 0 0 1-5 5h-2a2 2 0 0 0-2 2v3a2 2 0 0 1-1 0z",
        ["Dark Mode"]       = "M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z",
        ["Deployment"]      = "M22 12h-4l-3 9L9 3l-3 9H2",
        ["Reference"]       = "M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H20v20H6.5a2.5 2.5 0 0 1 0-5H20",
        ["API"]             = "M8 3H5a2 2 0 0 0-2 2v3 M21 8V5a2 2 0 0 0-2-2h-3 M3 16v3a2 2 0 0 0 2 2h3 M16 21h3a2 2 0 0 0 2-2v-3",
        ["Blog"]            = "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6",
        ["Changelog"]       = "M12 8v4l3 3 M22 12a10 10 0 1 1-10-10",
        ["Showcase"]        = "M12 2l3 7 7 1-5 5 1 7-6-3-6 3 1-7-5-5 7-1z"
    };

    public static string? GetPath(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        return _map.TryGetValue(title.Trim(), out var d) ? d : null;
    }
}
