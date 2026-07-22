using System.Reflection;
using Xunit;

namespace ShellDocs.Tests;

/* Integration tests for `shelldocs add`. Uses reflection to reach the internal
   AddCommand.Run entry point (matches the InitCommand test pattern). */
public class AddCommandTests : IDisposable
{
    private readonly string _sandbox;
    private readonly MethodInfo _run;

    public AddCommandTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), "shelldocs-add-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_sandbox, "content"));

        var cli = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "shelldocs")
            ?? Assembly.Load("shelldocs");
        var type = cli.GetType("ShellDocs.CLI.Commands.AddCommand", throwOnError: true)!;
        _run = type.GetMethod("Run", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    }

    public void Dispose()
    {
        try { Directory.Delete(_sandbox, recursive: true); } catch { }
    }

    private int Add(string template, string name, bool force = false, string? dir = null) =>
        (int)_run.Invoke(null, new object?[] { template, name, dir ?? _sandbox, force })!;

    [Fact]
    public void Add_Component_WritesToContentDocsComponents_WithSlugFilename()
    {
        var exit = Add("component", "MyBigCard");

        Assert.Equal(0, exit);
        var expected = Path.Combine(_sandbox, "content", "docs", "components", "my-big-card.md");
        Assert.True(File.Exists(expected));
        var body = File.ReadAllText(expected);
        Assert.Contains("title: MyBigCard", body);
        Assert.Contains("`<MyBigCard>`", body);
        Assert.Contains("razor:preview", body);
        Assert.Contains("<TypeTable>", body);
    }

    [Fact]
    public void Add_Guide_WritesToContentDocsGuides_WithTitleCaseName()
    {
        var exit = Add("guide", "getting-started");

        Assert.Equal(0, exit);
        var expected = Path.Combine(_sandbox, "content", "docs", "guides", "getting-started.md");
        Assert.True(File.Exists(expected));
        var body = File.ReadAllText(expected);
        Assert.Contains("title: Getting Started", body);
        Assert.Contains("<Steps>", body);
    }

    [Fact]
    public void Add_Page_WritesToContentDocs_WithBlankBody()
    {
        var exit = Add("page", "faq");

        Assert.Equal(0, exit);
        var expected = Path.Combine(_sandbox, "content", "docs", "faq.md");
        Assert.True(File.Exists(expected));
        var body = File.ReadAllText(expected);
        Assert.Contains("title: Faq", body);
        Assert.DoesNotContain("razor:preview", body);
    }

    [Fact]
    public void Add_UnknownTemplate_ReturnsNonZero()
    {
        Assert.NotEqual(0, Add("widget", "Foo"));
    }

    [Fact]
    public void Add_MissingContentDir_ReturnsNonZero()
    {
        var stray = Path.Combine(Path.GetTempPath(), "shelldocs-add-no-content-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(stray);
        try
        {
            Assert.NotEqual(0, Add("component", "Foo", dir: stray));
        }
        finally
        {
            Directory.Delete(stray, recursive: true);
        }
    }

    [Fact]
    public void Add_ExistingFileWithoutForce_Fails()
    {
        Add("component", "Duplicate");
        Assert.NotEqual(0, Add("component", "Duplicate"));
    }

    [Fact]
    public void Add_ExistingFileWithForce_Overwrites()
    {
        Add("component", "Duplicate");
        var target = Path.Combine(_sandbox, "content", "docs", "components", "duplicate.md");
        File.WriteAllText(target, "custom content");

        var exit = Add("component", "Duplicate", force: true);

        Assert.Equal(0, exit);
        var body = File.ReadAllText(target);
        Assert.Contains("`<Duplicate>`", body);
        Assert.DoesNotContain("custom content", body);
    }

    [Fact]
    public void Add_EmptyName_ReturnsNonZero()
    {
        Assert.NotEqual(0, Add("component", ""));
    }
}
