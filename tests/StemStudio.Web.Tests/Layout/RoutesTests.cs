using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Web.Components;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Layout;

public sealed class RoutesTests : PageTestContext
{
    [Fact]
    public void Root_RendersImportPageInsideLayout()
    {
        var routes = Render<Routes>();

        routes.Find("h1").TextContent.ShouldBe("Import a song");
        routes.Find("[data-testid=nav-tracks]").ShouldNotBeNull();
    }

    [Theory]
    [InlineData("import")]
    [InlineData("not-found")]
    public void UnknownRoute_RendersTheNotFoundPageInsideLayout(string route)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(route);

        var routes = Render<Routes>();

        routes.Find("h1").TextContent.ShouldBe("Page not found");
        routes.Find("[data-testid=not-found]").TextContent.ShouldContain("does not exist");
        routes.Find("[data-testid=not-found-home]").GetAttribute("href").ShouldBe("");
        routes.Find("[data-testid=nav-tracks]").ShouldNotBeNull();
    }

    [Fact]
    public void TracksRoute_RendersTrackList()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([]);
        Services.GetRequiredService<NavigationManager>().NavigateTo("tracks");

        var routes = Render<Routes>();

        routes.Find("h1").TextContent.ShouldBe("Tracks");
    }
}
