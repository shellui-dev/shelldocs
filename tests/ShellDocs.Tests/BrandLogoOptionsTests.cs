using ShellDocs.Components;
using Xunit;

namespace ShellDocs.Tests;

public class BrandLogoOptionsTests
{
    [Fact]
    public void SetLogo_SingleUrl_AppliesToBothThemes()
    {
        var options = new ShellDocsOptions().SetLogo("/img/logo.svg");

        Assert.Equal("/img/logo.svg", options.LogoLight);
        Assert.Equal("/img/logo.svg", options.LogoDark);
    }

    [Fact]
    public void SetLogo_LightAndDark_KeepsBothDistinct()
    {
        var options = new ShellDocsOptions().SetLogo("/img/light.svg", "/img/dark.svg", "Brand");

        Assert.Equal("/img/light.svg", options.LogoLight);
        Assert.Equal("/img/dark.svg", options.LogoDark);
        Assert.Equal("Brand", options.LogoAlt);
    }

    [Fact]
    public void LogoHeight_DefaultsToOnePointThreeSevenFiveRem()
    {
        Assert.Equal(1.375, new ShellDocsOptions().LogoHeight);
    }

    [Fact]
    public void Logos_DefaultToNull_SoDotFallbackRenders()
    {
        var options = new ShellDocsOptions();
        Assert.Null(options.LogoLight);
        Assert.Null(options.LogoDark);
        Assert.Null(options.LogoSvg);
    }

    [Fact]
    public void LogoSvg_HoldsRawMarkupUntouched()
    {
        const string svg = "<svg viewBox=\"0 0 24 24\"><path d=\"M0 0h24v24H0z\" fill=\"currentColor\"/></svg>";
        var options = new ShellDocsOptions { LogoSvg = svg };

        Assert.Equal(svg, options.LogoSvg);
    }
}
