using Bunit;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.BandInfo;

/// <summary>Band info entry points: track detail and mixer, lookup status, album line, legal notice (005 T041, FR-009, FR-014).</summary>
public sealed class BandInfoHostTests : PageTestContext
{
    public BandInfoHostTests()
    {
        Engine.LoadTrackAsync(Arg.Any<TrackDto>()).Returns(213.04);
        Api.GetBandInfoAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(ArtworkData.Found());
    }

    private IRenderedComponent<TrackDetail> RenderDetail(TrackDto first, params TrackDto[] next)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(first, next);
        return Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));
    }

    private IRenderedComponent<MixerPage> RenderMixer(TrackDto first, params TrackDto[] next)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(first, next);
        return Render<MixerPage>(p => p.Add(c => c.Id, TestData.TrackId));
    }

    [Theory]
    [InlineData(MetadataStatusDto.None, "none", "Band information not looked up yet")]
    [InlineData(MetadataStatusDto.Pending, "pending", "Waiting to look up band information")]
    [InlineData(MetadataStatusDto.Searching, "searching", "Searching for band information")]
    [InlineData(MetadataStatusDto.Found, "found", "Band information found")]
    [InlineData(MetadataStatusDto.NotFound, "notFound", "No band information found")]
    [InlineData(MetadataStatusDto.Failed, "failed", "Band information could not be retrieved")]
    [InlineData(MetadataStatusDto.Disabled, "disabled", "Online lookups are switched off")]
    public void TrackDetail_ShowsTheLookupStatus(MetadataStatusDto status, string value, string text)
    {
        var element = RenderDetail(ArtworkData.WithMetadata(status)).Find("[data-testid=metadata-status]");

        element.GetAttribute("data-status").ShouldBe(value);
        element.TextContent.ShouldContain(text);
    }

    [Fact]
    public void TrackDetail_PollsWhileTheLookupIsPending()
    {
        var page = RenderDetail(ArtworkData.WithMetadata(MetadataStatusDto.Pending), ArtworkData.WithMetadata(MetadataStatusDto.Searching), ArtworkData.WithMetadata(MetadataStatusDto.Found));

        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=metadata-status]").GetAttribute("data-status").ShouldBe("searching"));
        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=metadata-status]").GetAttribute("data-status").ShouldBe("found"));
        Timer.Waits.ShouldBe(2);
    }

    [Fact]
    public async Task TrackDetail_OpenButtonTogglesThePanel()
    {
        var page = RenderDetail(TestData.Track());
        var open = page.Find("[data-testid=band-info-open]");
        open.TagName.ShouldBe("BUTTON");
        open.GetAttribute("aria-expanded").ShouldBe("false");
        open.GetAttribute("aria-controls").ShouldBe("band-info-panel");
        page.FindAll("[data-testid=band-info-panel]").ShouldBeEmpty();

        await open.ClickAsync(new());

        page.Find("[data-testid=band-info-panel]").GetAttribute("id").ShouldBe("band-info-panel");
        page.Find("[data-testid=band-info-open]").GetAttribute("aria-expanded").ShouldBe("true");
        await page.Find("[data-testid=band-info-close]").ClickAsync(new());
        page.FindAll("[data-testid=band-info-panel]").ShouldBeEmpty();
    }

    [Fact]
    public async Task TrackDetail_RefreshesTheTrackWhenThePanelSettles()
    {
        Api.GetBandInfoAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(ArtworkData.Status(MetadataStatusDto.Searching), ArtworkData.Found());
        var page = RenderDetail(TestData.Track(), ArtworkData.WithMetadata(MetadataStatusDto.Found) with { Artwork = ArtworkData.Artwork(ArtworkSourceDto.CoverArtArchive) });
        await page.Find("[data-testid=band-info-open]").ClickAsync(new());

        Timer.Tick();

        page.WaitForAssertion(() => page.Find("[data-testid=track-cover]").GetAttribute("data-artwork-source").ShouldBe("coverartarchive"));
        page.Find("[data-testid=metadata-status]").GetAttribute("data-status").ShouldBe("found");
    }

    [Fact]
    public async Task Mixer_HasTheOpenButtonAndTheSamePanel()
    {
        var page = RenderMixer(TestData.Track());

        await page.Find("[data-testid=band-info-open]").ClickAsync(new());

        page.Find("[data-testid=band-info-name]").TextContent.ShouldBe("The Fixture Band");
        await page.Find("[data-testid=band-info-open]").ClickAsync(new());
        page.FindAll("[data-testid=band-info-panel]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Synthetic Sessions", 2020, "Synthetic Sessions (2020)")]
    [InlineData("Synthetic Sessions", null, "Synthetic Sessions")]
    public void Mixer_ShowsAlbumAndYearWhenFound(string album, int? year, string expected) =>
        RenderMixer(ArtworkData.WithMetadata(MetadataStatusDto.Found, album, year)).Find("[data-testid=mixer-album]").TextContent.ShouldBe(expected);

    [Theory]
    [InlineData(MetadataStatusDto.Found, null)]
    [InlineData(MetadataStatusDto.NotFound, "Synthetic Sessions")]
    public void Mixer_HidesTheAlbumWithoutAFoundAlbum(MetadataStatusDto status, string? album) =>
        RenderMixer(ArtworkData.WithMetadata(status, album, 2020)).FindAll("[data-testid=mixer-album]").ShouldBeEmpty();

    [Fact]
    public async Task Mixer_PicksUpTheAlbumWhenThePanelSettles()
    {
        Api.GetBandInfoAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(ArtworkData.Status(MetadataStatusDto.Searching), ArtworkData.Found());
        var page = RenderMixer(ArtworkData.WithMetadata(MetadataStatusDto.Searching), ArtworkData.WithMetadata(MetadataStatusDto.Found, "Synthetic Sessions", 2020));
        await page.Find("[data-testid=band-info-open]").ClickAsync(new());

        Timer.Tick();

        page.WaitForAssertion(() => page.Find("[data-testid=mixer-album]").TextContent.ShouldBe("Synthetic Sessions (2020)"));
    }

    [Fact]
    public async Task Mixer_KeepsItsTrackWhenTheRefreshFails()
    {
        Api.GetBandInfoAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(ArtworkData.Status(MetadataStatusDto.Searching), ArtworkData.Found());
        var page = RenderMixer(TestData.Track());
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns<TrackDto>(_ => throw Problem(System.Net.HttpStatusCode.ServiceUnavailable, "Down"));
        await page.Find("[data-testid=band-info-open]").ClickAsync(new());

        Timer.Tick();

        page.WaitForAssertion(() => page.Find("[data-testid=band-info-name]").ShouldNotBeNull());
        page.Find("[data-testid=mixer-cover]").ShouldNotBeNull();
        page.FindAll("[data-testid=mixer-load-error]").ShouldBeEmpty();
    }

    [Fact]
    public void LegalNotice_MentionsOnlineLookupsWithItsHelpMarker()
    {
        Api.GetDeviceAsync(Arg.Any<CancellationToken>()).Returns(new StemStudio.Contracts.Health.ProcessingDeviceDto { Reachable = false });

        var notice = Render<Import>().Find("[data-testid=legal-notice]");

        notice.TextContent.ShouldContain("Pictures and band information are fetched from open data services");
        notice.TextContent.ShouldContain("never exported");
        notice.QuerySelector("[data-help-id=online-lookups]").ShouldNotBeNull();
    }

    [Fact]
    public void BandInfoTopics_Exist()
    {
        var catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

        catalog.TopicForAnchor("band-info")!.Id.ShouldBe("band-info");
        catalog.TopicForAnchor("online-lookups")!.Id.ShouldBe("online-lookups");
    }
}
