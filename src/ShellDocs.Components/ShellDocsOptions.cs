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
    // Absolute base URL ("https://shelldocs.dev") for sitemap.xml, robots.txt and
    // og:url in `shelldocs build`; those are skipped when unset.
    public string? SiteUrl { get; set; }
    // Where the preview ⋯ menu's "Report a bug" / "Suggest something" open a new issue
    // (title and body appended as query params). Defaults to GitHubRepo's issues/new;
    // with neither set, those items are hidden.
    public string? IssueTrackerUrl { get; set; }

    public string? LogoLight { get; set; }
    public string? LogoDark { get; set; }
    public string? LogoAlt { get; set; }
    public double LogoHeight { get; set; } = 1.375;
    // Rendered as MarkupString — must be trusted content the consumer authored, not user input.
    public string? LogoSvg { get; set; }
    public ShellDocsTheme Theme { get; set; } = ShellDocsTheme.Shadcn;
    public DocsLayoutVariant LayoutVariant { get; set; } = DocsLayoutVariant.TopNav;

    public List<NavLink> PrimaryNav { get; } = new();
    // 0 or 1 entries hides the sidebar package selector entirely.
    public List<DocsPackage> Packages { get; } = new();
    // Fewer than 2 hides the version selector; any entry scopes sidebar, prev/next,
    // search and breadcrumb to the current version.
    public List<DocsVersion> Versions { get; } = new();
    // Searched recursively for X.razor to show as <DemoPreview Component="X" />'s source.
    public string? DemoSourceRoot { get; set; }
    public List<Type> RegisteredComponents { get; } = new();
    // Markdown tag name per type, from RegisterComponent<T>(tagName). Last registration wins.
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

    // Registers every public, concrete, non-generic component in the assembly, skipping [ShellDocsIgnore].
    public ShellDocsOptions RegisterComponentsFromAssembly<TMarker>(Func<Type, bool>? filter = null)
        => RegisterComponentsFromAssembly(typeof(TMarker).Assembly, filter);

    
    public ShellDocsOptions RegisterComponentsFromAssembly<TMarker>(string namespacePrefix)
    {
        if (string.IsNullOrEmpty(namespacePrefix))
            throw new ArgumentException("namespacePrefix must be non-empty.", nameof(namespacePrefix));
        return RegisterComponentsFromAssembly(typeof(TMarker).Assembly,
            t => t.Namespace is not null && t.Namespace.StartsWith(namespacePrefix, StringComparison.Ordinal));
    }

    public ShellDocsOptions RegisterComponentsFromAssembly(Assembly assembly, string namespacePrefix)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));
        if (string.IsNullOrEmpty(namespacePrefix))
            throw new ArgumentException("namespacePrefix must be non-empty.", nameof(namespacePrefix));
        return RegisterComponentsFromAssembly(assembly,
            t => t.Namespace is not null && t.Namespace.StartsWith(namespacePrefix, StringComparison.Ordinal));
    }

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
            // Partially loadable assemblies still expose their resolvable types.
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

    // `id` replaces the "{version}" token in package RootUrls.
    public ShellDocsOptions AddVersion(string id, string label, string rootUrl, string? description = null, bool latest = false)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("id must be non-empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(rootUrl))
            throw new ArgumentException("rootUrl must be non-empty.", nameof(rootUrl));
        Versions.Add(new DocsVersion(id, label, rootUrl, description, latest));
        return this;
    }

    public ShellDocsOptions SetLogo(string url, string? alt = null)
    {
        LogoLight = url;
        LogoDark = url;
        if (alt is not null) LogoAlt = alt;
        return this;
    }

    public ShellDocsOptions SetLogo(string lightUrl, string darkUrl, string? alt = null)
    {
        LogoLight = lightUrl;
        LogoDark = darkUrl;
        if (alt is not null) LogoAlt = alt;
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
// RootUrl may contain the "{version}" token (see DocsPackage.VersionToken).
public record DocsPackage(string Id, string Title, string Description, string RootUrl, string? IconPath = null)
{
    public const string VersionToken = "{version}";
    public bool IsVersioned => RootUrl.Contains(VersionToken, StringComparison.Ordinal);
}

public record DocsVersion(string Id, string Label, string RootUrl, string? Description, bool IsLatest);

public enum ShellDocsTheme
{
    Shadcn,
    Fuma,
    Nextra
}

// TopNav: DocsHeader across the top, sidebar below. Sidebar: no top nav — brand,
// search and collapse live in a floating sidebar.
public enum DocsLayoutVariant
{
    TopNav = 0,
    Sidebar = 1
}
