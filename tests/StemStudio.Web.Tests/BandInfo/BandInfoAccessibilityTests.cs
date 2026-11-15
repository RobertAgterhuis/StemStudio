using Bunit;
using NSubstitute;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.BandInfo;

/// <summary>Accessibility and help review of feature 005 (T054): focus order, names, alt texts, help search.</summary>
public sealed class BandInfoAccessibilityTests : PageTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    private IRenderedComponent<TrackDetail> RenderDetail()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(ArtworkData.WithArtwork());
        Api.GetBandInfoAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(ArtworkData.Found());
        return Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));
    }

    [Fact]
    public async Task OpeningThePanel_FocusesItsHeading_ClosingReturnsFocusToTheButton()
    {
        var page = RenderDetail();

        await page.Find("[data-testid=band-info-open]").ClickAsync(new());
        page.Find("#band-info-heading").GetAttribute("tabindex").ShouldBe("-1");
        await page.Find("[data-testid=band-info-close]").ClickAsync(new());

        JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus").ShouldBe(2);
    }

    [Fact]
    public async Task EveryPanelControl_HasAnAccessibleName()
    {
        var page = RenderDetail();
        await page.Find("[data-testid=band-info-open]").ClickAsync(new());

        foreach (var control in page.Find("[data-testid=band-info-panel]").QuerySelectorAll("button, a"))
        {
            (control.GetAttribute("aria-label") ?? control.TextContent.Trim()).ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData("band", "band-info")]
    [InlineData("wikipedia", "band-info")]
    [InlineData("cover", "artwork")]
    [InlineData("album art", "artwork")]
    [InlineData("offline", "online-lookups")]
    public void HelpSearch_FindsTheFeature005Topics(string query, string expected) =>
        new HelpSearch(Catalog).Search(query)[0].TopicId.ShouldBe(expected);

    [Fact]
    public void Feature005Topics_AreReviewedAndShort()
    {
        foreach (var id in new[] { "artwork", "band-info", "online-lookups" })
        {
            var topic = Catalog.Topic(id).ShouldNotBeNull();
            topic.Reviewed.ShouldBe("005");
            topic.Markdown.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length.ShouldBeLessThanOrEqualTo(250, id);
        }
    }
}
