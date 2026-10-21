using Xunit;

namespace ShellDocs.Tests;

public class ThemeCssTests
{
    [Fact]
    public void BaseBorderRule_IsInACascadeLayer()
    {
        // Unlayered, it beat Tailwind's border-colour utilities (@layer utilities).
        var css = File.ReadAllText(Asset("shelldocs-theme.css"));
        Assert.Matches(@"@layer base \{\s*\*, \*::before, \*::after \{ box-sizing: border-box; border-color: var\(--border\); \}\s*\}", css);
    }

    private static string Asset(string name)
    {
        var testDir = Path.GetDirectoryName(typeof(ThemeCssTests).Assembly.Location)!;
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
