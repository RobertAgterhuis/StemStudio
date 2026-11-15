using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.BandInfo;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.BandInfo;

/// <summary>Correct title and artist for the lookup (005 T041, FR-009, US2-3/US2-4).</summary>
public sealed class CorrectMetadataTests : PageTestContext
{
    private IRenderedComponent<BandInfoPanel> RenderPanel(BandInfoDto first, params BandInfoDto[] next)
    {
        Api.GetBandInfoAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(first, next);
        return Render<BandInfoPanel>(b => b.Add(x => x.Track, TestData.Track()));
    }

    private static async Task OpenAsync(IRenderedComponent<BandInfoPanel> page) =>
        await page.Find("[data-testid=band-info-correct]").ClickAsync(new());

    [Fact]
    public async Task Dialog_IsPrefilledWithTheTrackNames()
    {
        var page = RenderPanel(ArtworkData.Status(MetadataStatusDto.NotFound));

        await OpenAsync(page);

        var dialog = page.Find("[role=dialog]");
        dialog.GetAttribute("aria-modal").ShouldBe("true");
        page.Find("[data-testid=metadata-correct-title]").GetAttribute("value").ShouldBe("Never Gonna Give You Up");
        page.Find("[data-testid=metadata-correct-artist]").GetAttribute("value").ShouldBe("Rick Astley");
        page.Find("[data-testid=metadata-correct-title]").GetAttribute("maxlength").ShouldBe("500");
    }

    [Theory]
    [InlineData("   ", "Rick Astley", "Enter a title of 1 to 500 characters.")]
    [InlineData("Hero of the Day", "", "Enter an artist of 1 to 500 characters.")]
    public async Task InvalidNames_AreRefusedBeforeAnyRequest(string title, string artist, string message)
    {
        var page = RenderPanel(ArtworkData.Status(MetadataStatusDto.NotFound));
        await OpenAsync(page);

        page.Find("[data-testid=metadata-correct-title]").Input(title);
        page.Find("[data-testid=metadata-correct-artist]").Input(artist);
        await page.Find("[data-testid=metadata-correct-submit]").ClickAsync(new());

        page.Find("[data-testid=metadata-correct-error]").TextContent.ShouldBe(message);
        await Api.DidNotReceive().LookupMetadataAsync(Arg.Any<Guid>(), Arg.Any<MetadataLookupRequest?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_SendsTheTrimmedNames_ClosesTheDialog_AndSearches()
    {
        Api.LookupMetadataAsync(TestData.TrackId, Arg.Any<MetadataLookupRequest?>(), Arg.Any<CancellationToken>())
            .Returns(ArtworkData.WithMetadata(MetadataStatusDto.Pending));
        var page = RenderPanel(ArtworkData.Status(MetadataStatusDto.NotFound), ArtworkData.Status(MetadataStatusDto.Searching));
        await OpenAsync(page);

        page.Find("[data-testid=metadata-correct-title]").Input("  Hero of the Day ");
        page.Find("[data-testid=metadata-correct-artist]").Input("Metallica");
        await page.Find("[data-testid=metadata-correct-submit]").ClickAsync(new());

        await Api.Received(1).LookupMetadataAsync(TestData.TrackId, new MetadataLookupRequest("Hero of the Day", "Metallica"), Arg.Any<CancellationToken>());
        page.FindAll("[role=dialog]").ShouldBeEmpty();
        page.WaitForAssertion(() => page.Find("[data-testid=band-info-status]").GetAttribute("data-status").ShouldBe("searching"));
    }

    [Fact]
    public async Task RefusedCorrection_StaysOpenWithTheReason()
    {
        Api.LookupMetadataAsync(TestData.TrackId, Arg.Any<MetadataLookupRequest?>(), Arg.Any<CancellationToken>())
            .Returns<TrackDto>(_ => throw Problem(HttpStatusCode.UnprocessableEntity, "Invalid", "The title is too long."));
        var page = RenderPanel(ArtworkData.Found());
        await OpenAsync(page);

        await page.Find("[data-testid=metadata-correct-submit]").ClickAsync(new());

        page.Find("[data-testid=metadata-correct-error]").TextContent.ShouldBe("The title is too long.");
        page.FindAll("[role=dialog]").Count.ShouldBe(1);
    }

    [Fact]
    public async Task CancelAndEscape_CloseTheDialogWithoutARequest()
    {
        var page = RenderPanel(ArtworkData.Status(MetadataStatusDto.Failed));
        await OpenAsync(page);

        await page.Find("[data-testid=metadata-correct-cancel]").ClickAsync(new());
        page.FindAll("[role=dialog]").ShouldBeEmpty();

        await OpenAsync(page);
        await page.Find("[role=dialog]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        page.FindAll("[role=dialog]").ShouldBeEmpty();
        await Api.DidNotReceive().LookupMetadataAsync(Arg.Any<Guid>(), Arg.Any<MetadataLookupRequest?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ATrackWithoutArtist_StartsWithAnEmptyArtist()
    {
        Api.GetBandInfoAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(ArtworkData.Status(MetadataStatusDto.NotFound));
        var page = Render<BandInfoPanel>(b => b.Add(x => x.Track, TestData.Track() with { Artist = null }));

        await OpenAsync(page);

        page.Find("[data-testid=metadata-correct-artist]").GetAttribute("value").ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task EnterInAField_Submits_OtherKeysDoNot()
    {
        Api.LookupMetadataAsync(TestData.TrackId, Arg.Any<MetadataLookupRequest?>(), Arg.Any<CancellationToken>())
            .Returns(ArtworkData.WithMetadata(MetadataStatusDto.Pending));
        var page = RenderPanel(ArtworkData.Status(MetadataStatusDto.NotFound));
        await OpenAsync(page);

        await page.Find("[data-testid=metadata-correct-artist]").KeyDownAsync(new KeyboardEventArgs { Key = "a" });
        await page.Find("[role=dialog]").KeyDownAsync(new KeyboardEventArgs { Key = "a" });
        await Api.DidNotReceive().LookupMetadataAsync(Arg.Any<Guid>(), Arg.Any<MetadataLookupRequest?>(), Arg.Any<CancellationToken>());
        await page.Find("[data-testid=metadata-correct-artist]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        await Api.Received(1).LookupMetadataAsync(TestData.TrackId, new MetadataLookupRequest("Never Gonna Give You Up", "Rick Astley"), Arg.Any<CancellationToken>());
    }
}
