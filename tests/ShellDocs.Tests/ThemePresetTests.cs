using System.Reflection;
using System.Text.RegularExpressions;
using ShellDocs.Components;
using ShellDocs.Components.Layouts;
using ShellDocs.Templates;
using Xunit;

namespace ShellDocs.Tests;

public class ThemePresetTests : IDisposable
{
    private readonly string _root;

    public ThemePresetTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-theme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "docs"));
        File.WriteAllText(Path.Combine(_root, "docs", "introduction.md"), "---\ntitle: Introduction\n---\n# Introduction\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private ComponentRenderHarness Harness(ShellDocsTheme theme) => new(o =>
    {
        o.ContentRoot = _root;
        o.Theme = theme;
    }, "http://localhost/docs/introduction");

    private static IEnumerable<string> PresetNames() => typeof(ShellDocsTheme)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.GetCustomAttribute<ObsoleteAttribute>() is null)
        .Select(f => f.Name.ToLowerInvariant());

    [Fact]
    public async Task Layouts_MarkThePage_WithTheChosenPreset()
    {
        Assert.Contains("data-shelldocs-theme=\"harbor\"", await Harness(ShellDocsTheme.Harbor).RenderAsync<DocsLayout>());
        Assert.Contains("data-shelldocs-theme=\"jacaranda\"", await Harness(ShellDocsTheme.Jacaranda).RenderAsync<HomeLayout>());
    }

    [Fact]
    public async Task Graphite_LeavesThePageUnmarked()
    {
        Assert.DoesNotContain("data-shelldocs-theme", await Harness(ShellDocsTheme.Graphite).RenderAsync<DocsLayout>());
#pragma warning disable CS0618 // the old names stay as aliases of Graphite
        Assert.Equal(ShellDocsTheme.Graphite, ShellDocsTheme.Fuma);
        Assert.Equal(ShellDocsTheme.Graphite, ShellDocsTheme.Shadcn);
#pragma warning restore CS0618
    }

    [Fact]
    public void TokensCss_HasALightAndDarkPalette_ForEveryPreset()
    {
        var css = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "ShellDocs.Tokens", "wwwroot", "tokens.css"));
        foreach (var name in PresetNames().Where(n => n != "graphite"))
        {
            var selector = Regex.Escape($":has(:where([data-shelldocs-theme=\"{name}\"]))");
            Assert.Matches(@"(?m)^:root" + selector + @" \{[^}]*--primary:", css);
            Assert.Matches(@"(?m)^:root\.dark" + selector + @" \{[^}]*--primary:", css);
        }
    }

    [Fact]
    public void InitTheme_TakesEveryPreset_AndWritesIt()
    {
        Assert.Equal(PresetNames(), ScaffoldTemplates.ThemePresets);
        Assert.Contains("    o.Theme = ShellDocsTheme.Savanna;", ScaffoldTemplates.ProgramAddShellDocs("Site", "", "savanna"));
        Assert.Contains("// o.Theme = ShellDocsTheme.Harbor;", ScaffoldTemplates.ProgramAddShellDocs("Site", ""));
    }
}
