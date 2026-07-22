using Microsoft.Extensions.DependencyInjection;
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

        /* Auto-register the shipped content primitives so `razor:preview` blocks
           in markdown can reference <Callout>, <Card>, <Steps>, <FileTree> etc.
           without the consumer calling RegisterComponent<T>() themselves.
           Dogfoods RegisterComponentsFromAssembly against our own Content
           namespace — new primitives added under Content/ auto-appear here
           without a maintainer edit to this file. Internal render machinery
           (MarkdownContent, PreviewFrame) opts out via [ShellDocsIgnore]. */
        options.RegisterComponentsFromAssembly<Callout>(t => t.Namespace == "ShellDocs.Components.Content");

        services.AddSingleton<TypeRegistry>(_ => options.BuildTypeRegistry());
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

        return services;
    }
}
