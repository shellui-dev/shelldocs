using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using ShellDocs.Components;
using ShellDocs.Components.Chrome;
using ShellDocs.Components.Content;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class MobileNavMarkupTests
{
    private static ComponentRenderHarness Harness() => new(o =>
    {
        o.ContentRoot = Path.Combine(Path.GetTempPath(), "shelldocs-no-content-" + Guid.NewGuid().ToString("N"));
        o.AddNavLink("Showcase", "/showcase");
        o.AddNavMenu("Docs", new NavMenuItem("Intro", "/docs/introduction"));
    });

    [Fact]
    public async Task Header_Hamburger_IsJsDriven_AndHomeGetsAMobileMenu()
    {
        var home = await Harness().RenderAsync<DocsHeader>(new() { ["MobileMenu"] = true });
        Assert.Contains("data-mobile-nav-toggle", home);
        Assert.DoesNotContain("blazor:onclick", home);
        Assert.Contains("mobile-nav-icon-open", home);
        Assert.Contains("mobile-nav-icon-close", home);
        Assert.Contains("docs-header-mobile-menu", home);
        Assert.Contains("href=\"/showcase\"", home);
        Assert.Contains("href=\"/docs/introduction\"", home);

        var docs = await Harness().RenderAsync<DocsHeader>();
        Assert.DoesNotContain("docs-header-mobile-menu", docs);
    }
}
