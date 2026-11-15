using Bunit;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Artwork;

/// <summary>Where the picture is shown: track list thumbnails, track detail cover, mixer header (005 T028, FR-006).</summary>
public sealed class ArtworkPlacementTests : PageTestContext
{
    private static readonly Guid OtherId = Guid.Parse("44444444-2222-3333-4444-555555555555");

    public ArtworkPlacementTests() => Engine.LoadTrackAsync(Arg.Any<TrackDto>()).Returns(213.04);

    [Fact]
    public void TrackList_ShowsAThumbnailOrPlaceholderPerRow()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([ArtworkData.WithArtwork(ArtworkSourceDto.Youtube), TestData.Track() with { Id = OtherId, Title = "Plain" }]);

        var page = Render<Tracks>();

        var thumb = page.Find($"[data-testid=track-row-{TestData.TrackId}] [data-testid=track-thumb-{TestData.TrackId}]");
        thumb.TagName.ShouldBe("IMG");
        thumb.GetAttribute("src").ShouldBe(ArtworkData.ThumbUrl());
        thumb.GetAttribute("data-artwork-source").ShouldBe("youtube");
        page.Find($"[data-testid=track-thumb-{OtherId}]").GetAttribute("data-artwork-source").ShouldBe("none");
    }

    [Theory]
    [InlineData(TrackStatusDto.Separating)]
    [InlineData(TrackStatusDto.Completed)]
    public void TrackDetail_ShowsTheCoverInTheHeaderWhileProcessingAndAfter(TrackStatusDto status)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track(status) with { Artwork = ArtworkData.Artwork() });

        var page = Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));

        var cover = page.Find(".ss-track-head [data-testid=track-cover]");
        cover.GetAttribute("src").ShouldBe(ArtworkData.CoverUrl());
        cover.GetAttribute("data-help-id").ShouldBe("artwork");
    }

    [Fact]
    public void TrackDetail_PicksUpAPictureThatArrivesWhilePolling()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns(TestData.Track(TrackStatusDto.Separating), TestData.Track() with { Artwork = ArtworkData.Artwork(ArtworkSourceDto.CoverArtArchive) });
        var page = Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));
        page.Find("[data-testid=track-cover]").GetAttribute("data-artwork-source").ShouldBe("none");

        Timer.Tick();

        page.WaitForAssertion(() => page.Find("[data-testid=track-cover]").GetAttribute("data-artwork-source").ShouldBe("coverartarchive"));
    }

    [Fact]
    public void Mixer_ShowsTheCoverInItsHeader()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(ArtworkData.WithArtwork(ArtworkSourceDto.Embedded));

        var page = Render<MixerPage>(p => p.Add(c => c.Id, TestData.TrackId));

        var cover = page.Find(".ss-console-head [data-testid=mixer-cover]");
        cover.GetAttribute("data-artwork-source").ShouldBe("embedded");
        cover.GetAttribute("data-help-id").ShouldBe("artwork");
    }

    [Fact]
    public void Mixer_WithoutAPicture_ShowsThePlaceholder()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track());

        var page = Render<MixerPage>(p => p.Add(c => c.Id, TestData.TrackId));

        page.Find("[data-testid=mixer-cover]").GetAttribute("data-artwork-source").ShouldBe("none");
    }
}
