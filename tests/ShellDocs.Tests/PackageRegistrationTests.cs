using ShellDocs.Components;
using Xunit;

namespace ShellDocs.Tests;

public class PackageRegistrationTests
{
    [Fact]
    public void AddPackage_FluentChain_AppendsInOrder()
    {
        var options = new ShellDocsOptions()
            .AddPackage("core",       "Core",       "Foundation.",  "/docs/core")
            .AddPackage("components", "Components", "Widgets.",     "/docs/components", iconPath: "M0 0h10v10H0z");

        Assert.Equal(2, options.Packages.Count);
        Assert.Equal("core",       options.Packages[0].Id);
        Assert.Equal("Core",       options.Packages[0].Title);
        Assert.Equal("/docs/core", options.Packages[0].RootUrl);
        Assert.Null(options.Packages[0].IconPath);
        Assert.Equal("components",         options.Packages[1].Id);
        Assert.Equal("M0 0h10v10H0z",      options.Packages[1].IconPath);
    }

    [Fact]
    public void Packages_DefaultToEmpty_SoSelectorHides()
    {
        var options = new ShellDocsOptions();
        Assert.Empty(options.Packages);
    }
}
