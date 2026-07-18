using ShellDocs.Components;
using ShellDocs.Preview.Components;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddShellDocs(o =>
{
    o.ContentRoot = Path.Combine(builder.Environment.ContentRootPath, "content");
    o.SiteName = "ShellDocs";
    o.SiteTagline = "the docs framework for .NET";
    o.GitHubRepo = "shellui-dev/shelldocs";
    // Try the new floating-sidebar variant. Flip to TopNav for the classic look.
    o.LayoutVariant = DocsLayoutVariant.Sidebar;
    o.AddNavMenu("Documentation",
        new NavMenuItem("Getting Started", "/docs/introduction",
            "Install, configure, and ship your first ShellDocs site.",
            "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.75\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M13 2 3 14h9l-1 8 10-12h-9l1-8z\"/></svg>"),
        new NavMenuItem("Markdown Pipeline", "/docs/markdown",
            "Frontmatter, MDX-style slots, and custom components.",
            "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.75\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M4 4h16v16H4z\"/><path d=\"M4 9h16M9 4v16\"/></svg>"),
        new NavMenuItem("Components", "/docs/components/callout",
            "Callouts, cards, tabs, code groups — the essentials.",
            "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.75\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><rect x=\"3\" y=\"3\" width=\"7\" height=\"7\" rx=\"1\"/><rect x=\"14\" y=\"3\" width=\"7\" height=\"7\" rx=\"1\"/><rect x=\"3\" y=\"14\" width=\"7\" height=\"7\" rx=\"1\"/><rect x=\"14\" y=\"14\" width=\"7\" height=\"7\" rx=\"1\"/></svg>"),
        new NavMenuItem("Theming", "/docs/theming",
            "Palette tokens, dark mode, and shadcn compatibility.",
            "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.75\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 3v18M3 12h18\"/></svg>")
    );
    o.AddNavLink("Showcase", "/showcase");
    o.AddNavLink("Blog", "/blog");

    // Demo components available for <razor:preview> blocks in markdown.
    o.RegisterComponent<ShellDocs.Preview.Components.Demos.Callout>();
});

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
