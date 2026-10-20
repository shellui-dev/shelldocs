using System.Reflection;
using Xunit;

namespace ShellDocs.Tests;

public class PreviewCommandTests : IDisposable
{
    private readonly string _root;
    private static readonly Type Command = (AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "shelldocs")
            ?? Assembly.Load("shelldocs"))
        .GetType("ShellDocs.CLI.Commands.PreviewCommand", throwOnError: true)!;

    public PreviewCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "shelldocs-preview-" + Guid.NewGuid().ToString("N"));
        Write("index.html");
        Write("404.html");
        Write("docs/introduction/index.html");
        Write("about.html");
        Write("_shelldocs/search.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private void Write(string relative)
    {
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, relative);
    }

    private (string? File, int Status) Resolve(string basePath, string path)
    {
        var result = Command.GetMethod("Resolve", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [_root, basePath, path])!;
        var tuple = ((string?, int))result;
        return (tuple.Item1 is null ? null : Path.GetRelativePath(_root, tuple.Item1).Replace('\\', '/'), tuple.Item2);
    }

    [Theory]
    [InlineData("/", "index.html")]
    [InlineData("/docs/introduction", "docs/introduction/index.html")]
    [InlineData("/docs/introduction/", "docs/introduction/index.html")]
    [InlineData("/about", "about.html")]
    [InlineData("/_shelldocs/search.json", "_shelldocs/search.json")]
    public void Resolve_ServesFiles_FolderIndexes_AndHtmlExtensions(string path, string expected)
    {
        Assert.Equal((expected, 200), Resolve("/", path));
    }

    [Theory]
    [InlineData("/docs/nope")]
    [InlineData("/../secret.txt")]
    [InlineData("/%2e%2e/secret.txt")]
    public void Resolve_UnknownOrOutside_Is404Html(string path)
    {
        Assert.Equal(("404.html", 404), Resolve("/", path));
    }

    [Fact]
    public void Resolve_UnderABasePath()
    {
        Assert.Equal(("index.html", 200), Resolve("/my-repo/", "/my-repo/"));
        Assert.Equal(("index.html", 200), Resolve("/my-repo/", "/my-repo"));
        Assert.Equal(("docs/introduction/index.html", 200), Resolve("/my-repo/", "/my-repo/docs/introduction"));
        Assert.Equal(("404.html", 404), Resolve("/my-repo/", "/docs/introduction"));
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("/", "/")]
    [InlineData("my-repo", "/my-repo/")]
    [InlineData("/my-repo", "/my-repo/")]
    [InlineData("/my-repo/", "/my-repo/")]
    public void NormalizeBase_AddsBothSlashes(string? input, string expected)
    {
        Assert.Equal(expected, (string)Command.GetMethod("NormalizeBase", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [input])!);
    }
}
