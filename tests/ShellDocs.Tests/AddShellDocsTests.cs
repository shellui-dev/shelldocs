using Microsoft.Extensions.DependencyInjection;
using ShellDocs.Components;
using ShellDocs.Core;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class AddShellDocsTests
{
    [Fact]
    public void AddShellDocs_RegistersCoreServices()
    {
        var services = new ServiceCollection();
        services.AddShellDocs(o => o.SiteName = "Test");
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<ShellDocsOptions>());
        Assert.NotNull(sp.GetService<TypeRegistry>());
        Assert.NotNull(sp.GetService<MarkdownRenderer>());
        Assert.NotNull(sp.GetService<NavigationGraph>());
    }

    [Fact]
    public void AddShellDocs_MissingContentRoot_ReturnsEmptyGraph()
    {
        var services = new ServiceCollection();
        services.AddShellDocs(o => o.ContentRoot = "does-not-exist-" + Guid.NewGuid().ToString("N"));
        var sp = services.BuildServiceProvider();

        var graph = sp.GetRequiredService<NavigationGraph>();
        Assert.Empty(graph.Root.Children);
    }

    [Fact]
    public void AddShellDocs_RegisteredComponents_FlowIntoTypeRegistry()
    {
        var services = new ServiceCollection();
        services.AddShellDocs(o => o.RegisterComponent<Callout>());
        var sp = services.BuildServiceProvider();

        var registry = sp.GetRequiredService<TypeRegistry>();
        Assert.Equal(typeof(Callout), registry.Resolve("Callout"));
    }

    [Fact]
    public void ShellDocsOptions_FluentAddNavLink_AppendsInOrder()
    {
        var options = new ShellDocsOptions()
            .AddNavLink("Docs", "/docs")
            .AddNavLink("Blog", "/blog");

        Assert.Equal(2, options.PrimaryNav.Count);
        Assert.Equal("Docs", options.PrimaryNav[0].Label);
        Assert.Equal("/blog", options.PrimaryNav[1].Href);
    }

    public class Callout : Microsoft.AspNetCore.Components.ComponentBase { }
}
