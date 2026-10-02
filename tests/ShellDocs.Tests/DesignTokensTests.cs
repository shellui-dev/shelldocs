using System.Reflection;
using Xunit;

namespace ShellDocs.Tests;

// Fails loudly if tokens.css is renamed, moved, or dropped from the package.
public class DesignTokensTests
{
    private static string LoadTokensCss()
    {
        var testAssembly = Assembly.GetExecutingAssembly().Location;
        var testDir = Path.GetDirectoryName(testAssembly)!;

        var candidates = new[]
        {
            Path.Combine(testDir, "wwwroot", "_content", "ShellDocs.Tokens", "tokens.css"),
            Path.Combine(testDir, "..", "..", "..", "..", "..", "src", "ShellDocs.Tokens", "wwwroot", "tokens.css")
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full)) return File.ReadAllText(full);
        }
        throw new FileNotFoundException(
            $"tokens.css not found — checked: {string.Join(", ", candidates.Select(Path.GetFullPath))}");
    }

    [Fact]
    public void TokensFile_Exists_AndIsNonEmpty()
    {
        var css = LoadTokensCss();
        Assert.NotEmpty(css);
        Assert.True(css.Length > 500, "tokens.css looks unexpectedly small");
    }

    [Theory]
    [InlineData("--background")]
    [InlineData("--foreground")]
    [InlineData("--primary")]
    [InlineData("--muted")]
    [InlineData("--accent")]
    [InlineData("--border")]
    [InlineData("--radius")]
    [InlineData("--sidebar-width")]
    [InlineData("--header-height")]
    [InlineData("--font-sans")]
    [InlineData("--font-mono")]
    [InlineData("--info")]
    [InlineData("--warning")]
    [InlineData("--error")]
    [InlineData("--success")]
    public void TokensFile_DefinesStableToken(string token)
    {
        var css = LoadTokensCss();
        Assert.Contains(token + ":", css);
    }

    [Fact]
    public void TokensFile_DefinesLightAndDarkRoots()
    {
        var css = LoadTokensCss();
        Assert.Contains(":root {", css);
        Assert.Contains(":root.dark {", css);
    }

    [Fact]
    public void TokensFile_DarkRoot_OverridesBackground()
    {
        // Both blocks must redefine --background so the .dark class actually flips it.
        var css = LoadTokensCss();
        var darkStart = css.IndexOf(":root.dark {", StringComparison.Ordinal);
        Assert.True(darkStart > 0);
        var darkBlock = css.Substring(darkStart);
        Assert.Contains("--background:", darkBlock);
        Assert.Contains("--foreground:", darkBlock);
    }
}
