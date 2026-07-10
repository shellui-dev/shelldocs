using ShellDocs.Markdown;
using Xunit;

namespace ShellDocs.Tests;

public class TypeRegistryTests
{
    public class Callout { }
    public class Button { }

    [Fact]
    public void Register_ByGeneric_UsesTypeName()
    {
        var reg = new TypeRegistry().Register<Callout>();
        Assert.Equal(typeof(Callout), reg.Resolve("Callout"));
    }

    [Fact]
    public void Register_ByExplicitName_Wins()
    {
        var reg = new TypeRegistry().Register("Cta", typeof(Button));
        Assert.Equal(typeof(Button), reg.Resolve("Cta"));
        Assert.Null(reg.Resolve("Button"));
    }

    [Fact]
    public void Resolve_UnknownTag_ReturnsNull()
    {
        var reg = new TypeRegistry();
        Assert.Null(reg.Resolve("Missing"));
        Assert.False(reg.IsRegistered("Missing"));
    }

    [Fact]
    public void Register_ReturnsSelf_ForChaining()
    {
        var reg = new TypeRegistry()
            .Register<Callout>()
            .Register<Button>();
        Assert.Equal(2, reg.All.Count);
    }
}
