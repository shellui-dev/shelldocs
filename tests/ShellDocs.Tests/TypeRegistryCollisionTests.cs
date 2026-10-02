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

public class TypeRegistryCollisionTests
{
    public class Card : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder b) => b.AddContent(0, "consumer card");
    }

    public class OtherCard : ComponentBase { }

    [Fact]
    public void ConsumerComponent_WinsOverBuiltIn_AndBuiltInKeepsDocsAlias()
    {
        var sp = new ServiceCollection().AddShellDocs(o => o.RegisterComponent<Card>()).BuildServiceProvider();
        var registry = sp.GetRequiredService<TypeRegistry>();

        Assert.Equal(typeof(Card), registry.Resolve("Card"));
        Assert.Equal(typeof(ShellDocs.Components.Content.Card), registry.Resolve("DocsCard"));
        Assert.Equal(typeof(Callout), registry.Resolve("Callout"));
        Assert.Equal(typeof(Callout), registry.Resolve("DocsCallout"));

        var collision = Assert.Single(registry.Collisions);
        Assert.Equal("Card", collision.TagName);
        Assert.Equal(typeof(ShellDocs.Components.Content.Card), collision.Replaced);
        Assert.Equal(typeof(Card), collision.Winner);
    }

    [Fact]
    public void Registry_RecordsRepointedTags_NotRepeats()
    {
        var registry = new TypeRegistry()
            .Register("Card", typeof(Card))
            .Register("Card", typeof(Card))
            .Register("Card", typeof(OtherCard));

        Assert.Equal(typeof(OtherCard), registry.Resolve("Card"));
        var c = Assert.Single(registry.Collisions);
        Assert.Equal((typeof(Card), typeof(OtherCard)), (c.Replaced, c.Winner));
    }
}
