using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ShellDocs.Components;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class ComponentAliasTests
{
    [Fact]
    public void RegisterComponent_GenericAliasOverload_RegistersUnderTagName()
    {
        var services = new ServiceCollection();
        services.AddShellDocs(o => o.RegisterComponent<AliasWidget>("Widget"));
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<TypeRegistry>();

        Assert.Equal(typeof(AliasWidget), registry.Resolve("Widget"));
        // The type's short name should NOT resolve when an alias is in effect.
        Assert.Null(registry.Resolve(nameof(AliasWidget)));
    }

    [Fact]
    public void RegisterComponent_TypeAliasOverload_RegistersUnderTagName()
    {
        var services = new ServiceCollection();
        services.AddShellDocs(o => o.RegisterComponent(typeof(AliasWidget), "W"));
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<TypeRegistry>();

        Assert.Equal(typeof(AliasWidget), registry.Resolve("W"));
    }

    [Fact]
    public void RegisterComponent_AliasOverload_RejectsBlankTagName()
    {
        var options = new ShellDocsOptions();
        Assert.Throws<ArgumentException>(() => options.RegisterComponent<AliasWidget>(""));
        Assert.Throws<ArgumentException>(() => options.RegisterComponent<AliasWidget>("   "));
    }

    [Fact]
    public void RegisterComponent_LastAliasWinsForSameType()
    {
        var services = new ServiceCollection();
        services.AddShellDocs(o =>
        {
            o.RegisterComponent<AliasWidget>("First");
            o.RegisterComponent<AliasWidget>("Second");
        });
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<TypeRegistry>();

        Assert.Equal(typeof(AliasWidget), registry.Resolve("Second"));
    }

    public class AliasWidget : ComponentBase { }
}
