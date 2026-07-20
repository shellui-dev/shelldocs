using ShellDocs.Markdown;

namespace ShellDocs.Components;

public class ShellDocsOptions
{
    public string ContentRoot { get; set; } = "content";
    public string SiteName { get; set; } = "";
    public string? SiteTagline { get; set; }
    public string? GitHubRepo { get; set; }
    public ShellDocsTheme Theme { get; set; } = ShellDocsTheme.Shadcn;
    public DocsLayoutVariant LayoutVariant { get; set; } = DocsLayoutVariant.TopNav;

    public List<NavLink> PrimaryNav { get; } = new();
    public List<Type> RegisteredComponents { get; } = new();

    public ShellDocsOptions RegisterComponent<T>() where T : Microsoft.AspNetCore.Components.ComponentBase
    {
        RegisteredComponents.Add(typeof(T));
        return this;
    }

    public ShellDocsOptions AddNavLink(string label, string href)
    {
        PrimaryNav.Add(new NavLink(label, href));
        return this;
    }

    public ShellDocsOptions AddNavMenu(string label, params NavMenuItem[] items)
    {
        PrimaryNav.Add(new NavLink(label, "#", Children: items.ToList()));
        return this;
    }

    internal TypeRegistry BuildTypeRegistry()
    {
        var registry = new TypeRegistry();
        foreach (var type in RegisteredComponents) registry.Register(type);
        return registry;
    }
}

public record NavLink(string Label, string Href, List<NavMenuItem>? Children = null);
public record NavMenuItem(string Label, string Href, string? Description = null, string? IconSvg = null);

public enum ShellDocsTheme
{
    Shadcn,
    Fuma,
    Nextra
}

/* Which docs-layout chrome to render. TopNav is the classic build (DocsHeader
   spanning the top + sidebar below). Sidebar drops the top nav and moves
   brand + search + collapse into the sidebar itself — floating shadcn
   sidebar-04 / Claude-Code aesthetic. */
public enum DocsLayoutVariant
{
    TopNav = 0,
    Sidebar = 1
}
