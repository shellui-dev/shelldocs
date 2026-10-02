using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShellDocs.Components.Chrome;
using ShellDocs.Components.Content;
using ShellDocs.Core;
using ShellDocs.Markdown;

namespace ShellDocs.Components;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShellDocs(this IServiceCollection services, Action<ShellDocsOptions>? configure = null)
    {
        var options = new ShellDocsOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddScoped<MobileNavState>();
        services.AddScoped<ThemeState>();
        services.AddScoped<SearchState>();
        services.AddScoped<SidebarCollapseState>();
        services.AddScoped<CodeGroupSyncState>();
        services.AddScoped<DocsPageState>();

        /* Built-in content primitives are always available to markdown. Render
           machinery (MarkdownContent, PreviewFrame) opts out via [ShellDocsIgnore]. */
        options.AddBuiltInComponents(typeof(Callout).Assembly, t => t.Namespace == "ShellDocs.Components.Content");

        services.AddSingleton<TypeRegistry>(sp =>
        {
            var registry = options.BuildTypeRegistry();
            LogCollisions(registry, options, sp.GetService<ILoggerFactory>()?.CreateLogger("ShellDocs"));
            return registry;
        });
        services.AddSingleton<MarkdownRenderer>(sp => new MarkdownRenderer(sp.GetRequiredService<TypeRegistry>()));

        services.AddSingleton<NavigationGraph>(_ =>
        {
            if (!Directory.Exists(options.ContentRoot))
            {
                return new NavigationGraph(new NavigationNode { Url = "/", Kind = NodeKind.Section });
            }
            return NavigationGraphBuilder.Build(options.ContentRoot);
        });
        services.AddSingleton<SearchIndex>(sp => SearchIndex.FromGraph(sp.GetRequiredService<NavigationGraph>()));
        services.AddSingleton<DocsVersionResolver>();
        services.AddSingleton<DocsRedirects>();
        if (options.EnableRedirects)
            services.AddTransient<IStartupFilter, DocsRedirectStartupFilter>();

        return services;
    }

    private static void LogCollisions(TypeRegistry registry, ShellDocsOptions options, ILogger? logger)
    {
        if (logger is null) return;
        foreach (var c in registry.Collisions)
        {
            if (options.BuiltInComponents.Contains(c.Replaced))
                logger.LogInformation("ShellDocs: <{Tag}> renders {Winner}; the built-in stays available as <{Alias}>.",
                    c.TagName, c.Winner.FullName, ShellDocsOptions.BuiltInAliasPrefix + c.Replaced.Name);
            else
                logger.LogWarning("ShellDocs: <{Tag}> is registered by both {Replaced} and {Winner}; the later one wins. Use RegisterComponent<T>(\"Alias\") to expose the other.",
                    c.TagName, c.Replaced.FullName, c.Winner.FullName);
        }
    }
}
