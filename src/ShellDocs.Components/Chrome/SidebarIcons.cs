using Icons = ShellIcons.Icons;

namespace ShellDocs.Components.Chrome;

internal static class SidebarIcons
{
    public const double StrokeWidth = 1.75;

    // Title → ShellIcons component, so authors needn't configure per-page icons.
    private static readonly Dictionary<string, Type> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Getting Started"] = typeof(Icons.Zap),
        ["Introduction"]    = typeof(Icons.Clock),
        ["Installation"]    = typeof(Icons.Download),
        ["Quickstart"]      = typeof(Icons.Zap),
        ["Quick Start"]     = typeof(Icons.Zap),
        ["Project Structure"] = typeof(Icons.Folder),
        ["Configuration"]   = typeof(Icons.Settings),
        ["Authoring"]       = typeof(Icons.PenLine),
        ["Frontmatter"]     = typeof(Icons.FileText),
        ["Fenced Code"]     = typeof(Icons.Code),
        ["Razor Preview"]   = typeof(Icons.Layers),
        ["Razor:preview"]   = typeof(Icons.Layers),
        ["Inline Component Tags"] = typeof(Icons.CodeXml),
        ["Navigation"]      = typeof(Icons.Menu),
        ["Markdown"]        = typeof(Icons.Table),
        ["Components"]      = typeof(Icons.LayoutGrid),
        ["Callout"]         = typeof(Icons.CircleAlert),
        ["Card"]            = typeof(Icons.CreditCard),
        ["CardGrid"]        = typeof(Icons.LayoutGrid),
        ["Card Grid"]       = typeof(Icons.LayoutGrid),
        ["LinkCard"]        = typeof(Icons.Link),
        ["Link Card"]       = typeof(Icons.Link),
        ["Steps"]           = typeof(Icons.List),
        ["FileTree"]        = typeof(Icons.Folder),
        ["File Tree"]       = typeof(Icons.Folder),
        ["TypeTable"]       = typeof(Icons.Table),
        ["Type Table"]      = typeof(Icons.Table),
        ["Tabs"]            = typeof(Icons.PanelTop),
        ["Code Group"]      = typeof(Icons.Code),
        ["CodeGroup"]       = typeof(Icons.Code),
        ["PreviewFrame"]    = typeof(Icons.Video),
        ["Preview Frame"]   = typeof(Icons.Video),
        ["ComponentPreview"] = typeof(Icons.Video),
        ["Component Preview"] = typeof(Icons.Video),
        ["CLI"]             = typeof(Icons.Terminal),
        ["Cli"]             = typeof(Icons.Terminal),
        ["shelldocs init"]  = typeof(Icons.Plus),
        ["shelldocs add"]   = typeof(Icons.Plus),
        ["shelldocs dev"]   = typeof(Icons.ChartNoAxesColumn),
        ["shelldocs build"] = typeof(Icons.Wrench),
        ["Init"]            = typeof(Icons.Plus),
        ["Add"]             = typeof(Icons.Plus),
        ["Dev"]             = typeof(Icons.ChartNoAxesColumn),
        ["Build"]           = typeof(Icons.Wrench),
        ["Packages"]        = typeof(Icons.Package),
        ["Theming"]         = typeof(Icons.Palette),
        ["Palette"]         = typeof(Icons.Palette),
        ["Dark Mode"]       = typeof(Icons.Moon),
        ["Deployment"]      = typeof(Icons.Activity),
        ["Reference"]       = typeof(Icons.Book),
        ["API"]             = typeof(Icons.Scan),
        ["Blog"]            = typeof(Icons.File),
        ["Changelog"]       = typeof(Icons.History),
        ["Showcase"]        = typeof(Icons.Star)
    };

    public static Type? Get(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        return _map.TryGetValue(title.Trim(), out var icon) ? icon : null;
    }

    public static readonly IDictionary<string, object> ItemParameters = new Dictionary<string, object>
    {
        ["Class"] = "sidebar-item-icon",
        ["StrokeWidth"] = StrokeWidth
    };
}
