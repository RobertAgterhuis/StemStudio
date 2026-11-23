using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using static StemStudio.Api.Tests.InstrumentViews.AnalysisTestData;

namespace StemStudio.Api.Tests.InstrumentViews;

/// <summary>Feature 016 T123: <c>getCapoSuggestions</c> and the capo on the sheet and ChordPro (FR-075..FR-077).</summary>
public sealed class CapoEndpointsTests : ApiTestBase
{
    private static ChordSegment Segment(int startMs, string label, int root, ChordQuality quality) =>
        new() { StartMs = startMs, EndMs = startMs + 2000, Label = label, Root = root, Quality = quality, Confidence = 0.9 };

    /// <summary>Eb - Ab - Bb - Eb in Eb major: no open shape without a capo, all open with capo 1 (D - G - A - D), three of four with capo 3.</summary>
    private static ChordChart EbChart() => Chart() with
    {
        Key = new SongKey(3, KeyMode.Major, 0.9, null),
        KeyRegions = [new KeyRegion(0, 8000, 3, KeyMode.Major, 0.9)],
        Segments =
        [
            new ChordSegment { StartMs = 0, EndMs = 1000, Label = "N", Confidence = 0.9 },
            Segment(1000, "Eb:maj", 3, ChordQuality.Maj),
            Segment(3000, "Ab:maj", 8, ChordQuality.Maj),
            Segment(5000, "Bb:maj", 10, ChordQuality.Maj),
            Segment(7000, "Eb:maj", 3, ChordQuality.Maj),
        ],
    };

    private async Task<CapoSuggestionsDto> SuggestionsAsync(Guid trackId, int transpose)
    {
        using var response = await Client.GetAsync(new Uri(ApiRoutes.CapoSuggestions(trackId, transpose), UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getCapoSuggestions");
        return await response.BodyAsync<CapoSuggestionsDto>();
    }

    [Fact]
    public async Task Suggestions_RankTheCaposByOpenShapes_BestFirst()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.SeedReadyAsync(track.Id, EbChart(), new string('1', 64));

        var dto = await SuggestionsAsync(track.Id, 0);

        (dto.Transpose, dto.Shortcut).ShouldBe((0, null));
        dto.Suggestions.Count.ShouldBe(3);
        dto.Suggestions[0].ShouldBe(new CapoSuggestionDto { Capo = 1, ShapeKey = "D major", OpenShare = 1.0, FewOpenShapes = false });
        dto.Suggestions.Select(suggestion => suggestion.Capo).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task ATranspose_GivesTheShortcutThatKeepsTheOriginalShapes()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.SeedReadyAsync(track.Id, Chart(), new string('1', 64));

        var up = await SuggestionsAsync(track.Id, 2);
        var down = await SuggestionsAsync(track.Id, -3);
        var far = await SuggestionsAsync(track.Id, -5);

        up.Shortcut.ShouldBe(new CapoShortcutDto { Capo = 2, Reason = "original-shapes" });
        down.Shortcut.ShouldBeNull("12 - 3 = 9 is above the capo limit of 7");
        far.Shortcut.ShouldBe(new CapoShortcutDto { Capo = 7, Reason = "original-shapes" });
    }

    [Fact]
    public async Task WithoutReadyChords_Is409_And404ForAnUnknownTrack()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var notReady = await Client.GetAsync(new Uri(ApiRoutes.CapoSuggestions(track.Id, 0), UriKind.Relative), Ct);
        using var unknown = await Client.GetAsync(new Uri(ApiRoutes.CapoSuggestions(Guid.NewGuid(), 0), UriKind.Relative), Ct);

        notReady.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(notReady, "getCapoSuggestions");
        (await notReady.JsonAsync()).GetProperty("type").GetString().ShouldBe($"https://stemstudio.local/problems/{InstrumentViewProblemTypes.ChordsNotReady}");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(-13)]
    public async Task ATransposeOutOfRange_Is422(int transpose)
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.SeedReadyAsync(track.Id, EbChart(), new string('1', 64));

        using var response = await Client.GetAsync(new Uri(ApiRoutes.CapoSuggestions(track.Id, transpose), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "getCapoSuggestions");
        (await response.JsonAsync()).GetProperty("errors").TryGetProperty("transpose", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task TheBestCapo_OnTheSheetAndInChordPro_ShowsTheOpenShapes()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.SeedReadyAsync(track.Id, EbChart(), new string('1', 64));
        var best = (await SuggestionsAsync(track.Id, 0)).Suggestions[0].Capo;

        var sheet = await Client.GetFromJsonAsync<ChordSheetDto>(ApiRoutes.ChordSheet(track.Id, 0, best), ApiFactory.Json, Ct);
        var chordPro = await Client.GetStringAsync(new Uri(ApiRoutes.ChordPro(track.Id, 0, best), UriKind.Relative), Ct);

        sheet!.Rows.SelectMany(row => row.Chords).Select(chord => chord.Symbol).ShouldBe(["D", "G", "A", "D"]);
        chordPro.ShouldContain("{capo: 1}\n");
        chordPro.ShouldContain("[D]");
    }

    [Fact]
    public void AChartWithoutAKey_NamesTheShapesInCMajor_AndSkipsTheNoChordSegments()
    {
        Api.InstrumentViews.CapoEndpoints.KeyOf(Chart() with { Key = null }).ShouldBe(new MusicalKey(0, KeyMode.Major));
        Api.InstrumentViews.CapoEndpoints.Timed(EbChart()).Select(chord => chord.Root).ShouldBe([3, 8, 10, 3]);
    }
}
