using Microsoft.Extensions.DependencyInjection;
using ShellDocs.Components.Chrome;
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

        return services;
    }
}
