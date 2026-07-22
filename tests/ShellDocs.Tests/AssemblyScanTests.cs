using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ShellDocs.Components;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class AssemblyScanTests
{
    [Fact]
    public void RegisterComponentsFromAssembly_GenericMarker_PicksUpConcretePublicComponents()
    {
        var options = new ShellDocsOptions();
        options.RegisterComponentsFromAssembly<TestMarker>();

        Assert.Contains(typeof(ScannableAlpha), options.RegisteredComponents);
        Assert.Contains(typeof(ScannableBeta), options.RegisteredComponents);
    }

    [Fact]
    public void RegisterComponentsFromAssembly_SkipsAbstractTypes()
    {
        var options = new ShellDocsOptions();
        options.RegisterComponentsFromAssembly<TestMarker>();

        Assert.DoesNotContain(typeof(AbstractShouldSkip), options.RegisteredComponents);
    }

    [Fact]
    public void RegisterComponentsFromAssembly_SkipsGenericDefinitions()
    {
        var options = new ShellDocsOptions();
        options.RegisterComponentsFromAssembly<TestMarker>();

        Assert.DoesNotContain(options.RegisteredComponents, t => t.Name.StartsWith("GenericShouldSkip"));
    }

    [Fact]
    public void RegisterComponentsFromAssembly_SkipsNonComponentBase()
    {
        var options = new ShellDocsOptions();
        options.RegisterComponentsFromAssembly<TestMarker>();

        Assert.DoesNotContain(typeof(NotAComponent), options.RegisteredComponents);
    }

    [Fact]
    public void RegisterComponentsFromAssembly_RespectsShellDocsIgnore()
    {
        var options = new ShellDocsOptions();
        options.RegisterComponentsFromAssembly<TestMarker>();

        Assert.DoesNotContain(typeof(IgnoredExplicitly), options.RegisteredComponents);
    }

    [Fact]
    public void RegisterComponentsFromAssembly_FilterOverload_AppliedOnTopOfSystemRules()
    {
        var options = new ShellDocsOptions();
        options.RegisterComponentsFromAssembly<TestMarker>(t => t.Name == nameof(ScannableAlpha));

        Assert.Contains(typeof(ScannableAlpha), options.RegisteredComponents);
        Assert.DoesNotContain(typeof(ScannableBeta), options.RegisteredComponents);
    }

    [Fact]
    public void RegisterComponentsFromAssembly_FlowsIntoTypeRegistry()
    {
        var services = new ServiceCollection();
        services.AddShellDocs(o => o.RegisterComponentsFromAssembly<TestMarker>());
        var sp = services.BuildServiceProvider();

        var registry = sp.GetRequiredService<TypeRegistry>();
        Assert.Equal(typeof(ScannableAlpha), registry.Resolve(nameof(ScannableAlpha)));
        Assert.Equal(typeof(ScannableBeta), registry.Resolve(nameof(ScannableBeta)));
    }

    [Fact]
    public void RegisterComponent_TypeOverload_RejectsNonComponentBase()
    {
        var options = new ShellDocsOptions();
        Assert.Throws<ArgumentException>(() => options.RegisterComponent(typeof(NotAComponent)));
    }

    // ---- test doubles used by the scan (all live in this assembly) ----
    public class TestMarker { }
    public class ScannableAlpha : ComponentBase { }
    public class ScannableBeta : ComponentBase { }
    public abstract class AbstractShouldSkip : ComponentBase { }
    public class GenericShouldSkip<T> : ComponentBase { }
    public class NotAComponent { }
    [ShellDocsIgnore] public class IgnoredExplicitly : ComponentBase { }
}
