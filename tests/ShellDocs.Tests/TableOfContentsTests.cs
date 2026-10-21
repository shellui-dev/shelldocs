using ShellDocs.Components.Chrome;
using ShellDocs.Core;
using Xunit;

namespace ShellDocs.Tests;

// Under <Routes @rendermode="InteractiveServer" /> the TOC used to lose its scroll-spy
// and its #id links went to the home page.
public class TableOfContentsTests
{
    private static readonly Heading[] Headings =
    [
        new(2, "Install", "install"),
        new(3, "From NuGet", "from-nuget"),
        new(4, "Too deep", "too-deep"),
    ];

    private static Task<string> Render(string uri) =>
        new ComponentRenderHarness(uri: uri).RenderAsync<TableOfContents>(new() { ["Headings"] = Headings });

    [Fact]
    public async Task Links_CarryThePagePath()
    {
        var html = await Render("http://localhost/docs/guides/setup");

        // Base-relative: a bare #id resolves against <base href> to the home page.
        Assert.Contains("class=\"toc-link\" href=\"docs/guides/setup#install\" data-toc-id=\"install\"", html);
        Assert.Contains("href=\"docs/guides/setup#from-nuget\"", html);
        Assert.DoesNotContain("too-deep", html);
        Assert.Contains("data-toc-ids=\"install,from-nuget\"", html);
        Assert.DoesNotContain("href=\"#", html);
    }

    [Fact]
    public void ShelldocsJs_ReattachesTheScrollSpy_AndHandlesInPageLinks()
    {
        var js = File.ReadAllText(Asset("shelldocs.js"));
        Assert.Contains("attributeFilter: ['data-toc-ids']", js);
        Assert.Contains("if (touchesToc(added[n])) queueToc();", js);
        Assert.Contains("location.pathname + location.search + '#' + id", js);
        Assert.Contains("var samePage = raw.charAt(0) === '#'", js);
        Assert.Contains("window.addEventListener('scroll', onScroll, { passive: true });", js);
    }

    private static string Asset(string name)
    {
        var testDir = Path.GetDirectoryName(typeof(TableOfContentsTests).Assembly.Location)!;
        foreach (var candidate in new[]
        {
            Path.Combine(testDir, "wwwroot", "_content", "ShellDocs.Components", name),
            Path.Combine(testDir, "..", "..", "..", "..", "..", "src", "ShellDocs.Components", "wwwroot", name)
        })
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full)) return full;
        }
        throw new FileNotFoundException(name);
    }
}
