using System.Reflection;
using Xunit;

namespace ShellDocs.Tests;

/* We don't shell out to `dotnet publish` in tests (slow + fragile). We test
   the two deterministic post-processing helpers in isolation: RewriteBaseHref
   and the recursive CopyDirectory. Everything else in BuildCommand.Run is
   glue around Process.Start, which is best verified by hand. */
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
        _rewrite = type.GetMethod("RewriteBaseHref", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        _copy    = type.GetMethod("CopyDirectory",   BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private void RewriteBaseHref(string indexPath, string href) =>
        _rewrite.Invoke(null, new object[] { indexPath, href });

    private void CopyDirectory(string source, string dest) =>
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
        RewriteBaseHref(path, href);
        Assert.Contains(expected, File.ReadAllText(path));
    }

    [Fact]
    public void RewriteBaseHref_LeavesOtherMarkupUntouched()
    {
        var path = Path.Combine(_tempDir, "index.html");
        var input = "<html><head><title>App</title><base href=\"/\" /><meta /></head><body></body></html>";
        File.WriteAllText(path, input);
        RewriteBaseHref(path, "/x/");
        var output = File.ReadAllText(path);
        Assert.Contains("<title>App</title>", output);
        Assert.Contains("<meta />", output);
        Assert.Contains("<base href=\"/x/\" />", output);
    }

    [Fact]
    public void CopyDirectory_CopiesNestedFiles()
    {
        var src = Path.Combine(_tempDir, "src");
        var dst = Path.Combine(_tempDir, "dst");
        Directory.CreateDirectory(Path.Combine(src, "sub", "deep"));
        File.WriteAllText(Path.Combine(src, "root.txt"), "root");
        File.WriteAllText(Path.Combine(src, "sub", "mid.txt"), "mid");
        File.WriteAllText(Path.Combine(src, "sub", "deep", "leaf.txt"), "leaf");

        CopyDirectory(src, dst);

        Assert.Equal("root", File.ReadAllText(Path.Combine(dst, "root.txt")));
        Assert.Equal("mid",  File.ReadAllText(Path.Combine(dst, "sub", "mid.txt")));
        Assert.Equal("leaf", File.ReadAllText(Path.Combine(dst, "sub", "deep", "leaf.txt")));
    }

    [Fact]
    public void CopyDirectory_OverwritesExistingFiles()
    {
        var src = Path.Combine(_tempDir, "src");
        var dst = Path.Combine(_tempDir, "dst");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(dst);
        File.WriteAllText(Path.Combine(src, "a.txt"), "new");
        File.WriteAllText(Path.Combine(dst, "a.txt"), "old");

        CopyDirectory(src, dst);

        Assert.Equal("new", File.ReadAllText(Path.Combine(dst, "a.txt")));
    }
}
