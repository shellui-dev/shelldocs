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
        ["Quick Start"]     = "M13 2 3 14h9l-1 8 10-12h-9l1-8z",
        ["Project Structure"] = "M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z",
        ["Configuration"]   = "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09a1.65 1.65 0 0 0-1-1.51 1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09a1.65 1.65 0 0 0 1.51-1 1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33h.01A1.65 1.65 0 0 0 10 3.09V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82v.01a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z",
        ["Authoring"]       = "M12 20h9 M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z",
        ["Frontmatter"]     = "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M9 13h6 M9 17h6",
        ["Fenced Code"]     = "M16 18l6-6-6-6 M8 6l-6 6 6 6",
        ["Razor Preview"]   = "M12 2 2 7l10 5 10-5-10-5z M2 17l10 5 10-5",
        ["Razor:preview"]   = "M12 2 2 7l10 5 10-5-10-5z M2 17l10 5 10-5",
        ["Inline Component Tags"] = "M6 8L2 12l4 4 M18 8l4 4-4 4 M14.5 4l-5 16",
        ["Navigation"]      = "M3 6h18 M3 12h18 M3 18h18",
        ["Markdown"]        = "M4 4h16v16H4z M4 9h16 M9 4v16",
        ["Components"]      = "M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z",
        ["Callout"]         = "M12 8v4 M12 16h.01 M22 12A10 10 0 1 1 12 2a10 10 0 0 1 10 10z",
        ["Card"]            = "M3 5h18v14H3z M3 10h18",
        ["CardGrid"]        = "M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z",
        ["Card Grid"]       = "M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z",
        ["LinkCard"]        = "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71 M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71",
        ["Link Card"]       = "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71 M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71",
        ["Steps"]           = "M4 6h.01 M4 12h.01 M4 18h.01 M9 6h11 M9 12h11 M9 18h11",
        ["FileTree"]        = "M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z",
        ["File Tree"]       = "M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z",
        ["TypeTable"]       = "M3 3h18v18H3z M3 9h18 M3 15h18 M9 3v18",
        ["Type Table"]      = "M3 3h18v18H3z M3 9h18 M3 15h18 M9 3v18",
        ["Tabs"]            = "M3 3h18v6H3z M3 13h8v8H3z M13 13h8v8h-8z",
        ["Code Group"]      = "m8 6-6 6 6 6 M16 6l6 6-6 6",
        ["CodeGroup"]       = "m8 6-6 6 6 6 M16 6l6 6-6 6",
        ["PreviewFrame"]    = "M23 7l-7 5 7 5V7z M14 5H3a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2z",
        ["Preview Frame"]   = "M23 7l-7 5 7 5V7z M14 5H3a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2z",
        ["ComponentPreview"] = "M23 7l-7 5 7 5V7z M14 5H3a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2z",
        ["Component Preview"] = "M23 7l-7 5 7 5V7z M14 5H3a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2z",
        ["CLI"]             = "M4 17l6-6-6-6 M12 19h8",
        ["Cli"]             = "M4 17l6-6-6-6 M12 19h8",
        ["shelldocs init"]  = "M12 5v14 M5 12h14",
        ["shelldocs add"]   = "M12 5v14 M5 12h14",
        ["shelldocs dev"]   = "M18 20V10 M12 20V4 M6 20v-6",
        ["shelldocs build"] = "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z",
        ["Init"]            = "M12 5v14 M5 12h14",
        ["Add"]             = "M12 5v14 M5 12h14",
        ["Dev"]             = "M18 20V10 M12 20V4 M6 20v-6",
        ["Build"]           = "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z",
        ["Packages"]        = "M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z M3.27 6.96 12 12.01l8.73-5.05 M12 22.08V12",
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
