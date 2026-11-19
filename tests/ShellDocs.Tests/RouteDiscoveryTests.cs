using System.Reflection;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ShellDocs.Tests;

public class RouteDiscoveryTests
{
    [Route("/route-fixture/icons")]
    [Route("route-fixture/gallery")]
    private sealed class RoutedFixture : ComponentBase { }

    [Route("/route-fixture/items/{Id:int}")]
    private sealed class ParameterRouteFixture : ComponentBase { }

    private static readonly Type Discovery = CliType("ShellDocs.CLI.Commands.RouteDiscovery");

    [Fact]
    public void ReadRouteTemplates_FindsPageRoutesInAnAssembly()
    {
        var templates = Invoke<IReadOnlyList<string>>("ReadRouteTemplates", typeof(RouteDiscoveryTests).Assembly.Location);

        Assert.Contains("/route-fixture/icons", templates);
        Assert.Contains("route-fixture/gallery", templates);
        Assert.Contains("/route-fixture/items/{Id:int}", templates);
    }

    [Fact]
    public void PrerenderableRoutes_SkipsParametersAndTemplatePages()
    {
        var routes = Invoke<IReadOnlyList<string>>("PrerenderableRoutes", (IEnumerable<string>)new[]
        {
            "/", "/icons", "gallery", "/docs/{*Path:nonfile}", "/items/{id}", "/Error", "/not-found", "/ICONS"
        });

        Assert.Equal(new[] { "/", "/icons", "/gallery" }, routes);
    }

    [Fact]
    public void AddNew_SkipsUrlsAlreadyListed()
    {
        var urls = new List<string> { "/", "/docs/introduction" };
        var add = CliType("ShellDocs.CLI.Commands.BuildCommand").GetMethod("AddNew", BindingFlags.Static | BindingFlags.NonPublic)!;

        var added = (int)add.Invoke(null, new object[] { urls, new[] { "/Docs/Introduction", "/icons" } })!;

        Assert.Equal(1, added);
        Assert.Equal(new[] { "/", "/docs/introduction", "/icons" }, urls);
    }

    [Theory]
    [InlineData("site", "site", true)]
    [InlineData("site/..", "site", true)]
    [InlineData("site/publish", "site", false)]
    [InlineData("site-out", "site", false)]
    public void ContainsPath_FlagsTheProjectFolderAndItsParents(string output, string project, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "shelldocs-contains");
        var contains = CliType("ShellDocs.CLI.Commands.BuildCommand").GetMethod("ContainsPath", BindingFlags.Static | BindingFlags.NonPublic)!;

        var result = (bool)contains.Invoke(null, new object[] { Path.Combine(root, output), Path.Combine(root, project) })!;

        Assert.Equal(expected, result);
    }

    private static T Invoke<T>(string method, object arg) =>
        (T)Discovery.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new[] { arg })!;

    private static Type CliType(string name)
    {
        var cli = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "shelldocs")
            ?? Assembly.Load("shelldocs");
        return cli.GetType(name, throwOnError: true)!;
    }
}
