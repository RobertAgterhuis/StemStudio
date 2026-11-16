using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Components.Songs;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Songs;

/// <summary>A song take imported as a track (010 T055, US4-1, FR-011, FR-013; ui-test-ids 1.9.0 track-lyrics-link, track-vocal-badge).</summary>
public sealed class SongTrackTests : PageTestContext
{
    private static TrackDto SongTrack(VocalLabelDto? vocal, Guid? sheetId, string? profile = null) => TestData.Track() with
    {
        Id = GenerationData.ImportedTrackId,
        Title = "Paper boats (take 1)",
        Origin = new TrackOriginDto
        {
            Kind = TrackOriginKindDto.Generated,
            GenerationId = SongData.SongId,
            TakeIndex = 1,
            AiGenerated = true,
            Vocal = vocal,
            VoiceProfileName = profile,
            LyricsSheetId = sheetId,
        },
    };

    private IRenderedComponent<TrackDetail> RenderTrack(TrackDto track)
    {
        Api.GetTrackAsync(track.Id, Arg.Any<CancellationToken>()).Returns(track);
        return Render<TrackDetail>(p => p.Add(c => c.Id, track.Id));
    }

    [Fact]
    public void ASongTrack_LinksToItsLyrics_AndShowsTheAiVoice()
    {
        var page = RenderTrack(SongTrack(VocalLabelDto.Synthetic, SongData.SheetId));

        page.Find("[data-testid=track-lyrics-link]").GetAttribute("href").ShouldBe($"lyrics?sheet={SongData.SheetId}");
        var badge = page.Find("[data-testid=track-vocal-badge]");
        (badge.GetAttribute("data-vocal"), badge.TextContent).ShouldBe(("synthetic", "AI voice"));
        page.Find("[data-testid=track-origin-badge]").TextContent.ShouldBe("AI-generated");
    }

    [Fact]
    public void AConvertedTrack_NamesTheProfile_AndATrackWithoutSheetHasNoLink()
    {
        var page = RenderTrack(SongTrack(VocalLabelDto.Converted, null, "My voice"));

        page.Find("[data-testid=track-vocal-badge]").TextContent.ShouldBe("AI voice: My voice");
        page.FindAll("[data-testid=track-lyrics-link]").ShouldBeEmpty();
    }

    [Fact]
    public void ARestyledTrack_HasNoVocalBadge()
    {
        var page = RenderTrack(SongTrack(null, null));

        page.FindAll("[data-testid=track-vocal-badge]").ShouldBeEmpty();
    }

    [Fact]
    public void TheLyricsLink_HighlightsTheSheetInTheList()
    {
        Api.ListLyricsSheetsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([SongData.Sheet()]);
        Services.GetRequiredService<NavigationManager>().NavigateTo($"lyrics?sheet={SongData.SheetId}");

        var page = Render<LyricsListPage>();

        page.Find($"[data-testid=lyrics-row-{SongData.SheetId}]").GetAttribute("aria-current").ShouldBe("true");
    }
}
