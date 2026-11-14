using Bunit;
using StemStudio.Web.Components.Pages;

namespace StemStudio.Web.Tests;

public sealed class HomePageTests
{
    [Fact]
    public void Home_RendersProductName()
    {
        using var context = new BunitContext();

        var page = context.Render<Home>();

        page.Find("h1").TextContent.ShouldBe("StemStudio");
    }
}
