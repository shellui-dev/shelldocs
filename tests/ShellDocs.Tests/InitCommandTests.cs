using System.Reflection;
using Xunit;

namespace ShellDocs.Tests;

/* Integration tests for `shelldocs init` — spin up a minimal Blazor csproj in a
   temp dir, invoke InitCommand.Run via reflection (it's internal), assert the
   scaffolding lands and is idempotent. */
public class InitCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MethodInfo _run;

    public InitCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "shelldocs-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var cli = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "shelldocs")
            ?? Assembly.Load("shelldocs");
        var type = cli.GetType("ShellDocs.CLI.Commands.InitCommand", throwOnError: true)!;
        _run = type.GetMethod("Run", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private void WriteBlazorCsproj(string content = null!) =>
        File.WriteAllText(Path.Combine(_tempDir, "TestApp.csproj"),
            content ?? """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.0" />
                  </ItemGroup>
                </Project>
                """);

    private int Invoke() => (int)_run.Invoke(null, new object[] { _tempDir, true, "shadcn" })!;

    [Fact]
    public void Init_MissingCsproj_ReturnsError()
    {
        var code = Invoke();
        Assert.Equal(1, code);
    }

    [Fact]
    public void Init_NonBlazorCsproj_ReturnsError()
    {
        File.WriteAllText(Path.Combine(_tempDir, "TestApp.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
        var code = Invoke();
        Assert.Equal(1, code);
    }

    [Fact]
    public void Init_ValidBlazorProject_ScaffoldsContentAndPage()
    {
        WriteBlazorCsproj();
        var code = Invoke();
        Assert.Equal(0, code);

        Assert.True(File.Exists(Path.Combine(_tempDir, "content", "docs", "introduction.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "content", "docs", "meta.json")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "Components", "Pages", "DocsPage.razor")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "SHELLDOCS_SETUP.md")));
    }

    [Fact]
    public void Init_AddsShellDocsPackagesToCsproj()
    {
        WriteBlazorCsproj();
        Invoke();
        var csproj = File.ReadAllText(Path.Combine(_tempDir, "TestApp.csproj"));
        Assert.Contains("ShellDocs.Components", csproj);
        Assert.Contains("ShellDocs.Tokens", csproj);
    }

    [Fact]
    public void Init_RunTwice_IsIdempotent()
    {
        WriteBlazorCsproj();
        Assert.Equal(0, Invoke());
        var csprojAfterFirst = File.ReadAllText(Path.Combine(_tempDir, "TestApp.csproj"));
        var mdAfterFirst = File.ReadAllText(Path.Combine(_tempDir, "content", "docs", "introduction.md"));

        Assert.Equal(0, Invoke());
        var csprojAfterSecond = File.ReadAllText(Path.Combine(_tempDir, "TestApp.csproj"));
        var mdAfterSecond = File.ReadAllText(Path.Combine(_tempDir, "content", "docs", "introduction.md"));

        Assert.Equal(csprojAfterFirst, csprojAfterSecond);
        Assert.Equal(mdAfterFirst, mdAfterSecond);
    }

    [Fact]
    public void Init_PreservesUserModifications_OnRerun()
    {
        WriteBlazorCsproj();
        Invoke();
        var mdPath = Path.Combine(_tempDir, "content", "docs", "introduction.md");
        File.WriteAllText(mdPath, "# My custom intro\n");

        Invoke();

        Assert.Equal("# My custom intro\n", File.ReadAllText(mdPath));
    }

    [Fact]
    public void Init_UsesExistingPagesDir_WhenPresent()
    {
        WriteBlazorCsproj();
        var altPages = Path.Combine(_tempDir, "Pages");
        Directory.CreateDirectory(altPages);
        Invoke();

        Assert.True(File.Exists(Path.Combine(altPages, "DocsPage.razor")));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "Components", "Pages")));
    }
}
