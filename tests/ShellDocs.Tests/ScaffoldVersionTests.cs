using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using ShellDocs.Components;
using ShellDocs.Components.Chrome;
using ShellDocs.Components.Content;
using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class ScaffoldVersionTests
{
    [Fact]
    public void InitScaffoldsTheCliOwnVersion()
    {
        var cli = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "shelldocs")
                  ?? Assembly.Load("shelldocs");
        var type = cli.GetType("ShellDocs.CLI.Commands.InitCommand", throwOnError: true)!;
        var version = (string)type.GetMethod("ResolveShellDocsVersion", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;

        Assert.DoesNotContain("+", version);
        Assert.Equal(ReadPropsVersion(), version);
    }

    private static string ReadPropsVersion()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var xml = File.ReadAllText(Path.Combine(dir!.FullName, "Directory.Build.props"));
        var start = xml.IndexOf("<Version>", StringComparison.Ordinal) + "<Version>".Length;
        return xml[start..xml.IndexOf("</Version>", start, StringComparison.Ordinal)];
    }
}
