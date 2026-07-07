using Microsoft.Extensions.DependencyInjection;
using ShellDocs.Core;

namespace ShellDocs.Components;

/// Registers ShellDocs services with the consumer's DI container.
/// Consumer's Program.cs calls this once — everything else is discovered by convention.
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShellDocs(
        this IServiceCollection services,
        Action<ShellDocsOptions>? configure = null)
    {
        var options = new ShellDocsOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // Real service wiring lands in feat/core-navigation-graph + feat/markdown-pipeline.
        // Scaffolding: keep the surface consumers will call, no implementation yet.
        return services;
    }
}

/// Fluent options bag consumers configure in Program.cs.
public class ShellDocsOptions
{
    public string ContentRoot { get; set; } = "content";
    public string SiteName { get; set; } = "";
    public string? GitHubRepo { get; set; }
    public bool EnableSearch { get; set; } = true;
    public string SearchIndexPath { get; set; } = "search-index.json";
    public List<Type> RegisteredComponents { get; } = new();

    public ShellDocsOptions RegisterComponent<T>() where T : Microsoft.AspNetCore.Components.ComponentBase
    {
        RegisteredComponents.Add(typeof(T));
        return this;
    }
}
