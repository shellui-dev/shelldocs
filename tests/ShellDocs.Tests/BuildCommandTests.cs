using System.Reflection;
using Xunit;

namespace ShellDocs.Tests;

public class BuildCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MethodInfo _rewrite;
    private readonly MethodInfo _copy;

    public BuildCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "shelldocs-build-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var cli = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "shelldocs")
            ?? Assembly.Load("shelldocs");
        var type = cli.GetType("ShellDocs.CLI.Commands.BuildCommand", throwOnError: true)!;
        _rewrite = type.GetMethod("RewriteBaseHrefInAllHtml", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        _copy    = type.GetMethod("CopyDirectoryMerging",    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private int RewriteBaseHrefInAllHtml(string outputDir, string href) =>
        (int)_rewrite.Invoke(null, new object[] { outputDir, href })!;

    private void CopyDirectoryMerging(string source, string dest) =>
        _copy.Invoke(null, new object[] { source, dest });

    [Theory]
    [InlineData("<base href=\"/\" />",         "/repo/", "<base href=\"/repo/\" />")]
    [InlineData("<base href='/'/>",            "/repo/", "<base href=\"/repo/\" />")]
    [InlineData("<base href=/ />",             "/repo/", "<base href=\"/repo/\" />")]
    [InlineData("<BASE HREF=\"/old/\" />",     "/new/",  "<base href=\"/new/\" />")]
    public void RewriteBaseHref_HandlesQuoteVariants(string original, string href, string expected)
    {
        var path = Path.Combine(_tempDir, "index.html");
        File.WriteAllText(path, $"<html><head>{original}</head></html>");
        var count = RewriteBaseHrefInAllHtml(_tempDir, href);
        Assert.Equal(1, count);
        Assert.Contains(expected, File.ReadAllText(path));
    }

    [Fact]
    public void RewriteBaseHref_LeavesOtherMarkupUntouched()
    {
        var path = Path.Combine(_tempDir, "index.html");
        var input = "<html><head><title>App</title><base href=\"/\" /><meta /></head><body></body></html>";
        File.WriteAllText(path, input);
        RewriteBaseHrefInAllHtml(_tempDir, "/x/");
        var output = File.ReadAllText(path);
        Assert.Contains("<title>App</title>", output);
        Assert.Contains("<meta />", output);
        Assert.Contains("<base href=\"/x/\" />", output);
    }

    [Fact]
    public void RewriteBaseHref_WalksAllHtmlFilesRecursively()
    {
        var root = Path.Combine(_tempDir, "index.html");
        var docs = Path.Combine(_tempDir, "docs", "introduction", "index.html");
        var deep = Path.Combine(_tempDir, "docs", "cli", "build", "index.html");
        Directory.CreateDirectory(Path.GetDirectoryName(docs)!);
        Directory.CreateDirectory(Path.GetDirectoryName(deep)!);
        File.WriteAllText(root, "<html><base href=\"/\" /></html>");
        File.WriteAllText(docs, "<html><base href=\"/\" /></html>");
        File.WriteAllText(deep, "<html><base href=\"/\" /></html>");

        var count = RewriteBaseHrefInAllHtml(_tempDir, "/repo/");

        Assert.Equal(3, count);
        Assert.Contains("<base href=\"/repo/\" />", File.ReadAllText(root));
        Assert.Contains("<base href=\"/repo/\" />", File.ReadAllText(docs));
        Assert.Contains("<base href=\"/repo/\" />", File.ReadAllText(deep));
    }

    [Fact]
    public void CopyDirectoryMerging_CopiesNestedFiles()
    {
        var src = Path.Combine(_tempDir, "src");
        var dst = Path.Combine(_tempDir, "dst");
        Directory.CreateDirectory(Path.Combine(src, "sub", "deep"));
        File.WriteAllText(Path.Combine(src, "root.txt"), "root");
        File.WriteAllText(Path.Combine(src, "sub", "mid.txt"), "mid");
        File.WriteAllText(Path.Combine(src, "sub", "deep", "leaf.txt"), "leaf");

        CopyDirectoryMerging(src, dst);

        Assert.Equal("root", File.ReadAllText(Path.Combine(dst, "root.txt")));
        Assert.Equal("mid",  File.ReadAllText(Path.Combine(dst, "sub", "mid.txt")));
        Assert.Equal("leaf", File.ReadAllText(Path.Combine(dst, "sub", "deep", "leaf.txt")));
    }

    [Fact]
    public void CopyDirectoryMerging_DoesNotOverwriteExistingFiles()
    {
        // Prerendered HTML in output/ must survive the wwwroot merge on top.
        var src = Path.Combine(_tempDir, "src");
        var dst = Path.Combine(_tempDir, "dst");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(dst);
        File.WriteAllText(Path.Combine(src, "index.html"), "asset-version");
        File.WriteAllText(Path.Combine(dst, "index.html"), "prerendered-version");

        CopyDirectoryMerging(src, dst);

        Assert.Equal("prerendered-version", File.ReadAllText(Path.Combine(dst, "index.html")));
    }

    [Fact]
    public void CopyDirectoryMerging_CopiesMissingFilesEvenWhenSomeExist()
    {
        var src = Path.Combine(_tempDir, "src");
        var dst = Path.Combine(_tempDir, "dst");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(dst);
        File.WriteAllText(Path.Combine(src, "a.txt"), "src-a");
        File.WriteAllText(Path.Combine(src, "b.txt"), "src-b");
        File.WriteAllText(Path.Combine(dst, "a.txt"), "dst-a");

        CopyDirectoryMerging(src, dst);

        Assert.Equal("dst-a", File.ReadAllText(Path.Combine(dst, "a.txt")));
        Assert.Equal("src-b", File.ReadAllText(Path.Combine(dst, "b.txt")));
    }
}
