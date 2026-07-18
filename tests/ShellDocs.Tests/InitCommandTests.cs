using System.Reflection;
using Xunit;

namespace ShellDocs.Tests;

/* Integration tests for `shelldocs init`. Covers ATTACH mode end-to-end
   (fast — no `dotnet new` spawn) and the CREATE-mode patchers (PatchProgramCs,
   PatchAppRazor) against synthetic fresh-blazor-template fixtures. The full
   CREATE path (dotnet new blazor + patchers) is verified by hand — spawning
   dotnet in unit tests is slow and fragile. */
public class InitCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MethodInfo _run;
    private readonly MethodInfo _patchProgram;
    private readonly MethodInfo _patchApp;
    private readonly MethodInfo _findSln;

    public InitCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "shelldocs-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var cli = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "shelldocs")
            ?? Assembly.Load("shelldocs");
        var type = cli.GetType("ShellDocs.CLI.Commands.InitCommand", throwOnError: true)!;
        _run          = type.GetMethod("Run",             BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        _patchProgram = type.GetMethod("PatchProgramCs",  BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        _patchApp     = type.GetMethod("PatchAppRazor",   BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        _findSln      = type.GetMethod("FindNearestSolution", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
    }

    private string? FindNearestSolution(string dir) => (string?)_findSln.Invoke(null, new object[] { dir });

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

    private int InvokeAttach() =>
        (int)_run.Invoke(null, new object?[] { null, _tempDir, true, true, "shadcn" })!;

    // ---- ATTACH MODE ----------------------------------------------------

    [Fact]
    public void Attach_MissingCsproj_ReturnsError()
    {
        Assert.Equal(1, InvokeAttach());
    }

    [Fact]
    public void Attach_NonBlazorCsproj_ReturnsError()
    {
        File.WriteAllText(Path.Combine(_tempDir, "TestApp.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
        Assert.Equal(1, InvokeAttach());
    }

    [Fact]
    public void Attach_ValidBlazorProject_ScaffoldsContentAndPage()
    {
        WriteBlazorCsproj();
        Assert.Equal(0, InvokeAttach());
        Assert.True(File.Exists(Path.Combine(_tempDir, "content", "docs", "introduction.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "content", "docs", "meta.json")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "Components", "Pages", "DocsPage.razor")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "SHELLDOCS_SETUP.md")));
    }

    [Fact]
    public void Attach_AddsShellDocsPackagesToCsproj()
    {
        WriteBlazorCsproj();
        InvokeAttach();
        var csproj = File.ReadAllText(Path.Combine(_tempDir, "TestApp.csproj"));
        Assert.Contains("ShellDocs.Components", csproj);
        Assert.Contains("ShellDocs.Tokens", csproj);
    }

    [Fact]
    public void Attach_RunTwice_IsIdempotent()
    {
        WriteBlazorCsproj();
        Assert.Equal(0, InvokeAttach());
        var csprojA = File.ReadAllText(Path.Combine(_tempDir, "TestApp.csproj"));
        Assert.Equal(0, InvokeAttach());
        var csprojB = File.ReadAllText(Path.Combine(_tempDir, "TestApp.csproj"));
        Assert.Equal(csprojA, csprojB);
    }

    [Fact]
    public void Attach_PreservesUserModifications_OnRerun()
    {
        WriteBlazorCsproj();
        InvokeAttach();
        var mdPath = Path.Combine(_tempDir, "content", "docs", "introduction.md");
        File.WriteAllText(mdPath, "# My custom intro\n");
        InvokeAttach();
        Assert.Equal("# My custom intro\n", File.ReadAllText(mdPath));
    }

    // ---- CREATE-MODE PATCHERS -------------------------------------------

    /// <summary>Emits a synthetic Program.cs identical in shape to `dotnet new blazor` output.</summary>
    private void WriteFreshBlazorProgramCs() =>
        File.WriteAllText(Path.Combine(_tempDir, "Program.cs"),
            """
            using TestApp.Components;

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error", createScopeForErrors: true);
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseAntiforgery();
            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
            """);

    private void WriteFreshBlazorAppRazor()
    {
        var componentsDir = Path.Combine(_tempDir, "Components");
        Directory.CreateDirectory(componentsDir);
        File.WriteAllText(Path.Combine(componentsDir, "App.razor"),
            """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <base href="/" />
                <link rel="stylesheet" href="@Assets["app.css"]" />
                <ImportMap />
                <link rel="icon" type="image/png" href="favicon.png" />
                <HeadOutlet />
            </head>
            <body>
                <Routes />
                <script src="_framework/blazor.web.js"></script>
            </body>
            </html>
            """);
    }

    [Fact]
    public void PatchProgramCs_InsertsUsingWebHostAndAddShellDocs()
    {
        WriteFreshBlazorProgramCs();
        var changes = new List<string>();
        _patchProgram.Invoke(null, new object[] { _tempDir, "MySite", "myorg/myrepo", changes });

        var src = File.ReadAllText(Path.Combine(_tempDir, "Program.cs"));
        Assert.Contains("using ShellDocs.Components;", src);
        Assert.Contains("builder.WebHost.UseStaticWebAssets();", src);
        Assert.Contains("builder.Services.AddShellDocs(", src);
        Assert.Contains("o.SiteName = \"MySite\"", src);
        Assert.Contains("o.GitHubRepo = \"myorg/myrepo\"", src);
        Assert.Single(changes);
    }

    [Fact]
    public void PatchProgramCs_IsIdempotent()
    {
        WriteFreshBlazorProgramCs();
        var changes = new List<string>();
        _patchProgram.Invoke(null, new object[] { _tempDir, "MySite", "", changes });
        var afterFirst = File.ReadAllText(Path.Combine(_tempDir, "Program.cs"));

        var secondChanges = new List<string>();
        _patchProgram.Invoke(null, new object[] { _tempDir, "MySite", "", secondChanges });
        var afterSecond = File.ReadAllText(Path.Combine(_tempDir, "Program.cs"));

        Assert.Equal(afterFirst, afterSecond);
        Assert.Empty(secondChanges);
    }

    [Fact]
    public void PatchAppRazor_InsertsCssLinksBootstrapAndScripts()
    {
        WriteFreshBlazorAppRazor();
        var changes = new List<string>();
        _patchApp.Invoke(null, new object[] { _tempDir, changes });

        var src = File.ReadAllText(Path.Combine(_tempDir, "Components", "App.razor"));
        Assert.Contains("_content/ShellDocs.Tokens/tokens.css", src);
        Assert.Contains("_content/ShellDocs.Components/shelldocs-theme.css", src);
        Assert.Contains("_content/ShellDocs.Components/shelldocs.js", src);
        Assert.Contains("import { createHighlighter } from 'https://esm.sh/shiki", src);
        Assert.Contains("localStorage.getItem('shelldocs-theme')", src);
        Assert.Single(changes);
    }

    [Fact]
    public void PatchAppRazor_IsIdempotent()
    {
        WriteFreshBlazorAppRazor();
        var changes = new List<string>();
        _patchApp.Invoke(null, new object[] { _tempDir, changes });
        var afterFirst = File.ReadAllText(Path.Combine(_tempDir, "Components", "App.razor"));

        var secondChanges = new List<string>();
        _patchApp.Invoke(null, new object[] { _tempDir, changes });
        var afterSecond = File.ReadAllText(Path.Combine(_tempDir, "Components", "App.razor"));

        Assert.Equal(afterFirst, afterSecond);
        Assert.Empty(secondChanges);
    }

    // ---- SOLUTION FINDER ------------------------------------------------

    [Fact]
    public void FindNearestSolution_ReturnsSlnxInSameDir()
    {
        var path = Path.Combine(_tempDir, "MyRepo.slnx");
        File.WriteAllText(path, "<Solution></Solution>");
        Assert.Equal(path, FindNearestSolution(_tempDir));
    }

    [Fact]
    public void FindNearestSolution_ReturnsSlnWhenNoSlnx()
    {
        var path = Path.Combine(_tempDir, "MyRepo.sln");
        File.WriteAllText(path, "Microsoft Visual Studio Solution File");
        Assert.Equal(path, FindNearestSolution(_tempDir));
    }

    [Fact]
    public void FindNearestSolution_PrefersSlnxOverSln()
    {
        File.WriteAllText(Path.Combine(_tempDir, "MyRepo.sln"),  "legacy");
        var slnx = Path.Combine(_tempDir, "MyRepo.slnx");
        File.WriteAllText(slnx, "<Solution></Solution>");
        Assert.Equal(slnx, FindNearestSolution(_tempDir));
    }

    [Fact]
    public void FindNearestSolution_WalksUpToParent()
    {
        var slnx = Path.Combine(_tempDir, "MyRepo.slnx");
        File.WriteAllText(slnx, "<Solution></Solution>");
        var sub = Path.Combine(_tempDir, "docs", "MyRepo.Docs");
        Directory.CreateDirectory(sub);
        Assert.Equal(slnx, FindNearestSolution(sub));
    }

    [Fact]
    public void FindNearestSolution_StopsAtGitRoot()
    {
        // .git in tempDir marks it as repo root; no sln inside means null,
        // even if an sln exists in tempDir's parent (which it doesn't here).
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));
        var sub = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(sub);
        Assert.Null(FindNearestSolution(sub));
    }

    [Fact]
    public void FindNearestSolution_ReturnsNullWhenNoneFound()
    {
        Assert.Null(FindNearestSolution(_tempDir));
    }
}
