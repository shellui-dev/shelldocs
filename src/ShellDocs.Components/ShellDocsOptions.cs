using System.Reflection;
using Microsoft.AspNetCore.Components;
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
    // 0 or 1 entries hides the sidebar package selector entirely.
    public List<DocsPackage> Packages { get; } = new();
    public List<Type> RegisteredComponents { get; } = new();
    /* Per-type tag alias — when a type appears here, BuildTypeRegistry uses this
       string as the markdown-facing tag name instead of the type's short name.
       Consumers use this to expose a component under a different name in docs
       (e.g. RegisterComponent<Button>("Btn")). Last-write-wins if the same type
       is registered under multiple aliases. */
    public Dictionary<Type, string> ComponentAliases { get; } = new();

    public ShellDocsOptions RegisterComponent<T>() where T : ComponentBase
    {
        RegisteredComponents.Add(typeof(T));
        return this;
    }

    public ShellDocsOptions RegisterComponent<T>(string tagName) where T : ComponentBase
    {
        if (string.IsNullOrWhiteSpace(tagName))
            throw new ArgumentException("tagName must be non-empty.", nameof(tagName));
        RegisteredComponents.Add(typeof(T));
        ComponentAliases[typeof(T)] = tagName;
        return this;
    }

    public ShellDocsOptions RegisterComponent(Type type)
    {
        if (!typeof(ComponentBase).IsAssignableFrom(type))
            throw new ArgumentException($"{type.FullName} must derive from ComponentBase.", nameof(type));
        RegisteredComponents.Add(type);
        return this;
    }

    public ShellDocsOptions RegisterComponent(Type type, string tagName)
    {
        if (!typeof(ComponentBase).IsAssignableFrom(type))
            throw new ArgumentException($"{type.FullName} must derive from ComponentBase.", nameof(type));
        if (string.IsNullOrWhiteSpace(tagName))
            throw new ArgumentException("tagName must be non-empty.", nameof(tagName));
        RegisteredComponents.Add(type);
        ComponentAliases[type] = tagName;
        return this;
    }

    /* Scan the assembly that TMarker lives in for all public, concrete,
       non-generic ComponentBase-derived types and register each. Skips types
       tagged with [ShellDocsIgnore]. Duplicate registrations against the same
       tag name are silently ignored downstream in TypeRegistry. */
    public ShellDocsOptions RegisterComponentsFromAssembly<TMarker>(Func<Type, bool>? filter = null)
        => RegisterComponentsFromAssembly(typeof(TMarker).Assembly, filter);

    public ShellDocsOptions RegisterComponentsFromAssembly(Assembly assembly, Func<Type, bool>? filter = null)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));
        foreach (var type in DiscoverComponentTypes(assembly))
        {
            if (filter is not null && !filter(type)) continue;
            RegisteredComponents.Add(type);
        }
        return this;
    }

    private static IEnumerable<Type> DiscoverComponentTypes(Assembly assembly)
    {
        Type[] types;
        try { types = assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            /* An assembly with a partially-loadable type surface still yields
               its resolvable types via the exception's Types property (nulls
               are the unresolvable ones). Salvage what we can. */
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }
        foreach (var t in types)
        {
            if (t is null) continue;
            if (!t.IsClass || t.IsAbstract) continue;
            if (!t.IsPublic && !t.IsNestedPublic) continue;
            if (t.IsGenericTypeDefinition) continue;
            if (!typeof(ComponentBase).IsAssignableFrom(t)) continue;
            if (t.IsDefined(typeof(ShellDocsIgnoreAttribute), inherit: false)) continue;
            yield return t;
        }
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

    public ShellDocsOptions AddPackage(string id, string title, string description, string rootUrl, string? iconPath = null)
    {
        Packages.Add(new DocsPackage(id, title, description, rootUrl, iconPath));
        return this;
    }

    internal TypeRegistry BuildTypeRegistry()
    {
        var registry = new TypeRegistry();
        foreach (var type in RegisteredComponents)
        {
            if (ComponentAliases.TryGetValue(type, out var alias))
                registry.Register(alias, type);
            else
                registry.Register(type);
        }
        return registry;
    }
}

public record NavLink(string Label, string Href, List<NavMenuItem>? Children = null);
public record NavMenuItem(string Label, string Href, string? Description = null, string? IconSvg = null);

// IconPath is a raw SVG `d` attribute value on a 24×24 viewBox — not a URL.
public record DocsPackage(string Id, string Title, string Description, string RootUrl, string? IconPath = null);

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
