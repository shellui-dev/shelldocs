using System.Reflection;
using ShellDocs.Core;
using Xunit;

namespace ShellDocs.Tests;

public class BuildCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MethodInfo _rewrite;
    private readonly MethodInfo _copy;
    private readonly MethodInfo _writeSitemap;
    private readonly MethodInfo _writeRobots;
    private readonly MethodInfo _injectOg;

    public BuildCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "shelldocs-build-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var cli = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "shelldocs")
            ?? Assembly.Load("shelldocs");
        var type = cli.GetType("ShellDocs.CLI.Commands.BuildCommand", throwOnError: true)!;
        _rewrite       = type.GetMethod("RewriteBaseHrefInAllHtml", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        _copy          = type.GetMethod("CopyDirectoryMerging",    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        _writeSitemap  = type.GetMethod("WriteSitemap",            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        _writeRobots   = type.GetMethod("WriteRobots",             BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        _injectOg      = type.GetMethod("InjectOgMeta",            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private int RewriteBaseHrefInAllHtml(string outputDir, string href) =>
        (int)_rewrite.Invoke(null, new object[] { outputDir, href })!;

    private void CopyDirectoryMerging(string source, string dest) =>
        _copy.Invoke(null, new object[] { source, dest });

    private void WriteSitemap(string outputDir, string siteUrl, IReadOnlyList<string> urls) =>
        _writeSitemap.Invoke(null, new object[] { outputDir, siteUrl, urls });

    private void WriteRobots(string outputDir, string siteUrl) =>
        _writeRobots.Invoke(null, new object[] { outputDir, siteUrl });

    private int InjectOgMeta(string outputDir, string siteUrl, NavigationGraph graph) =>
        (int)_injectOg.Invoke(null, new object[] { outputDir, siteUrl, graph })!;

    // NavigationNode.Children/Parent have `internal set` — bypass via reflection
    // so tests can build a graph without exposing the setters or standing up a
    // temp content directory.
    private static readonly PropertyInfo _childrenProp = typeof(NavigationNode).GetProperty("Children")!;
    private static readonly PropertyInfo _parentProp = typeof(NavigationNode).GetProperty("Parent")!;
    private static void LinkChildren(NavigationNode parent, params NavigationNode[] children)
    {
        _childrenProp.SetValue(parent, children);
        foreach (var c in children) _parentProp.SetValue(c, parent);
    }

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

    [Fact]
    public void WriteSitemap_ProducesValidUrlSet()
    {
        WriteSitemap(_tempDir, "https://example.com", new[] { "/", "/docs/introduction", "/docs/cli/build" });

        var xml = File.ReadAllText(Path.Combine(_tempDir, "sitemap.xml"));
        Assert.Contains("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", xml);
        Assert.Contains("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">", xml);
        Assert.Contains("<loc>https://example.com/</loc>", xml);
        Assert.Contains("<loc>https://example.com/docs/introduction</loc>", xml);
        Assert.Contains("<loc>https://example.com/docs/cli/build</loc>", xml);
        Assert.Contains("</urlset>", xml);
    }

    [Fact]
    public void WriteSitemap_DeduplicatesUrls()
    {
        WriteSitemap(_tempDir, "https://example.com", new[] { "/", "/", "/docs/x", "/docs/x" });

        var xml = File.ReadAllText(Path.Combine(_tempDir, "sitemap.xml"));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(xml, "<url>").Count);
    }

    [Fact]
    public void WriteRobots_IncludesSitemapReference()
    {
        WriteRobots(_tempDir, "https://example.com");
        var txt = File.ReadAllText(Path.Combine(_tempDir, "robots.txt"));

        Assert.Contains("User-agent: *", txt);
        Assert.Contains("Allow: /", txt);
        Assert.Contains("Sitemap: https://example.com/sitemap.xml", txt);
    }

    [Fact]
    public void InjectOgMeta_AddsOgTagsBeforeHeadClose()
    {
        var pagePath = Path.Combine(_tempDir, "docs", "intro", "index.html");
        Directory.CreateDirectory(Path.GetDirectoryName(pagePath)!);
        File.WriteAllText(pagePath, "<html><head><title>t</title></head><body>b</body></html>");

        var pageNode = new NavigationNode
        {
            Url = "/docs/intro",
            Title = "Introduction",
            Description = "What ShellDocs is.",
            Kind = NodeKind.Page,
        };
        var root = new NavigationNode { Url = "/", Kind = NodeKind.Section };
        LinkChildren(root, pageNode);
        var graph = new NavigationGraph(root);

        var count = InjectOgMeta(_tempDir, "https://example.com", graph);

        Assert.Equal(1, count);
        var html = File.ReadAllText(pagePath);
        Assert.Contains("<meta property=\"og:type\" content=\"article\" />", html);
        Assert.Contains("<meta property=\"og:url\" content=\"https://example.com/docs/intro\" />", html);
        Assert.Contains("<meta property=\"og:title\" content=\"Introduction\" />", html);
        Assert.Contains("<meta property=\"og:description\" content=\"What ShellDocs is.\" />", html);
        // Injected before </head>, not after.
        var ogIdx = html.IndexOf("og:type", StringComparison.Ordinal);
        var closeIdx = html.IndexOf("</head>", StringComparison.Ordinal);
        Assert.True(ogIdx > 0 && ogIdx < closeIdx, "og:type meta must appear before </head>");
    }

    [Fact]
    public void InjectOgMeta_SkipsMissingHtmlFilesGracefully()
    {
        var pageNode = new NavigationNode
        {
            Url = "/nowhere",
            Title = "Ghost",
            Description = "Not on disk.",
            Kind = NodeKind.Page,
        };
        var root = new NavigationNode { Url = "/", Kind = NodeKind.Section };
        LinkChildren(root, pageNode);
        var graph = new NavigationGraph(root);

        var count = InjectOgMeta(_tempDir, "https://example.com", graph);

        Assert.Equal(0, count);
    }

    [Fact]
    public void InjectOgMeta_EncodesSpecialCharacters()
    {
        var pagePath = Path.Combine(_tempDir, "p", "index.html");
        Directory.CreateDirectory(Path.GetDirectoryName(pagePath)!);
        File.WriteAllText(pagePath, "<html><head></head></html>");

        var pageNode = new NavigationNode
        {
            Url = "/p",
            Title = "AT&T <spec>",
            Description = "Uses & and <",
            Kind = NodeKind.Page,
        };
        var root = new NavigationNode { Url = "/", Kind = NodeKind.Section };
        LinkChildren(root, pageNode);
        var graph = new NavigationGraph(root);

        InjectOgMeta(_tempDir, "https://example.com", graph);

        var html = File.ReadAllText(pagePath);
        Assert.Contains("AT&amp;T &lt;spec&gt;", html);
        Assert.Contains("Uses &amp; and &lt;", html);
    }
}
