using Bunit;
using NSubstitute;
using StemStudio.Contracts.Generation;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Generation;

/// <summary>The track page section and the generations page (009 T043, US1-1, FR-007; ui-test-ids 1.8.0).</summary>
public sealed class RestyleEntryTests : GenerationTestContext
{
    private IRenderedComponent<TrackDetail> RenderTrackPage()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track());
        return Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));
    }

    [Fact]
    public void TrackPage_HasAGenerationsSection_WithTheTracksRestyles()
    {
        Api.ListGenerationsAsync(new GenerationQuery(GenerationKindDto.Restyle, TestData.TrackId), Arg.Any<CancellationToken>()).Returns([GenerationData.Generation()]);

        var page = RenderTrackPage();

        var section = page.Find("[data-testid=track-generations]");
        section.QuerySelector($"[data-testid=generation-row-{GenerationData.GenerationId}]").ShouldNotBeNull();
        page.Find("[data-testid=track-restyle]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Restyle_OpensTheDialog_AndAStartedGenerationAppearsInTheList()
    {
        var page = RenderTrackPage();

        await page.Find("[data-testid=track-restyle]").ClickAsync(new());
        page.Find("[data-testid=restyle-dialog]").ShouldNotBeNull();
        await page.Find("[data-testid=restyle-start]").ClickAsync(new());

        page.FindAll("[data-testid=restyle-dialog]").ShouldBeEmpty();
        page.Find($"[data-testid=generation-row-{GenerationData.GenerationId}]").GetAttribute("data-status").ShouldBe("queued");
    }

    [Fact]
    public async Task TheDialog_CanBeClosedWithoutStarting()
    {
        var page = RenderTrackPage();

        await page.Find("[data-testid=track-restyle]").ClickAsync(new());
        await page.Find("[data-testid=restyle-cancel]").ClickAsync(new());

        page.FindAll("[data-testid=restyle-dialog]").ShouldBeEmpty();
        await Api.DidNotReceive().CreateRestyleAsync(Arg.Any<Guid>(), Arg.Any<RestyleRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GenerationsPage_ListsEveryGeneration_WithItsLimitsHelp()
    {
        Api.ListGenerationsAsync(new GenerationQuery(), Arg.Any<CancellationToken>()).Returns([GenerationData.Generation()]);

        var page = Render<Generations>();

        page.Find("h1").TextContent.ShouldBe("Generations");
        page.Find("[data-help-id=generation-limits]").ShouldNotBeNull();
        page.Find($"[data-testid=generation-row-{GenerationData.GenerationId}]").ShouldNotBeNull();
        page.Find("[data-testid=generations-limits]").TextContent.ShouldContain("quality varies");
    }

    [Fact]
    public void Navigation_LinksToGenerationsAndStyles()
    {
        var nav = Render<StemStudio.Web.Components.Layout.NavMenu>();

        nav.Find("[data-testid=nav-generations]").GetAttribute("href").ShouldBe("generations");
        nav.Find("[data-testid=nav-styles]").GetAttribute("href").ShouldBe("styles");
    }
}
