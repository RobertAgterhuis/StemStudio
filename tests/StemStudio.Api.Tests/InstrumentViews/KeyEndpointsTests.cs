using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using static StemStudio.Api.Tests.InstrumentViews.AnalysisTestData;

namespace StemStudio.Api.Tests.InstrumentViews;

/// <summary>Feature 016 T116: <c>getKey</c> and the <c>Track.key</c> summary (FR-070..FR-074).</summary>
public sealed class KeyEndpointsTests : ApiTestBase
{
    /// <summary>A completed four-stem track with downbeats every 2 s from 0.5 s (bar 9 starts at 16.5 s).</summary>
    private static Track GridTrack(bool reliable, string videoId = "dQw4w9WgXcQ")
    {
        var track = SyncTestData.UrlTrack(videoId);
        var downbeats = Enumerable.Range(0, 20).Select(bar => 0.5 + (2.0 * bar)).ToList();
        var grid = new BeatGrid { Bpm = 120, Beats = downbeats, Downbeats = downbeats, Confidence = reliable ? 0.9 : 0.2, Reliable = reliable };
        track.Complete(new TrackCompletion(180, "htdemucs", StemCatalog.FourStem.Select(name => new Stem(name, 10)).ToList(), grid), T0);
        return track;
    }

    private static ChordChart Uncertain() => Chart() with { Key = new SongKey(9, KeyMode.Minor, 0.4, new MusicalKey(0, KeyMode.Major)) };

    private async Task<KeyViewDto> KeyAsync(string route)
    {
        using var response = await Client.GetAsync(new Uri(route, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getKey");
        return await response.BodyAsync<KeyViewDto>();
    }

    [Fact]
    public async Task ReadyChords_GiveTheKeyAndTheRegionsWithTheirBars()
    {
        var track = await Factory.SeedAsync(GridTrack(reliable: true));
        await Factory.SeedReadyAsync(track.Id, Chart(), new string('1', 64));

        var view = await KeyAsync(ApiRoutes.Key(track.Id, 0, null));

        (view.Status, view.Transpose, view.Display, view.WrittenKey).ShouldBe((AnalysisStatusDto.Ready, 0, "C major", null));
        view.Key.ShouldBe(new SongKeyDto { Tonic = 0, Mode = KeyModeDto.Major, Confidence = 0.81, Alternative = null });
        view.Regions.Select(region => (region.StartMs, region.StartBar, region.Display)).ShouldBe([(500, (int?)1, "C major"), (16500, 9, "D major")]);
    }

    [Fact]
    public async Task ATranspose_SpellsTheKeyAndTheRegions_WithTheOriginal()
    {
        var track = await Factory.SeedAsync(GridTrack(reliable: true));
        await Factory.SeedReadyAsync(track.Id, Chart(), new string('1', 64));

        var view = await KeyAsync(ApiRoutes.Key(track.Id, 2, null));

        (view.Transpose, view.Display).ShouldBe((2, "D major (original C major, +2)"));
        view.Regions.Select(region => region.Display).ShouldBe(["D major", "E major"]);
        view.Key!.Tonic.ShouldBe(0, "the key itself stays at concert pitch");
    }

    [Theory]
    [InlineData("alto-sax-eb", 0, "A major")]
    [InlineData("trumpet-bb", 0, "D major")]
    [InlineData("horn-f", 2, "A major")]
    [InlineData("concert", 0, null)]
    public async Task AnInstrument_GivesTheWrittenKeyOfTheTransposedKey(string instrument, int transpose, string? written)
    {
        var track = await Factory.SeedAsync(GridTrack(reliable: true));
        await Factory.SeedReadyAsync(track.Id, Chart(), new string('1', 64));

        var view = await KeyAsync(ApiRoutes.Key(track.Id, transpose, instrument));

        view.WrittenKey.ShouldBe(written);
    }

    [Fact]
    public async Task WithoutAReliableGrid_TheBarsAreNull_AndALowConfidenceIsUncertain()
    {
        var track = await Factory.SeedAsync(GridTrack(reliable: false));
        await Factory.SeedReadyAsync(track.Id, Uncertain(), new string('1', 64));

        var view = await KeyAsync(ApiRoutes.Key(track.Id, 0, null));

        view.Regions.ShouldAllBe(region => region.StartBar == null);
        view.Display.ShouldBe("A minor (uncertain)");
        view.Key!.Alternative.ShouldBe(new KeyAlternativeDto { Tonic = 0, Mode = KeyModeDto.Major });
    }

    [Fact]
    public async Task AStaleChart_StillGivesTheKey()
    {
        var track = await Factory.SeedAsync(GridTrack(reliable: true));
        await Factory.SeedReadyAsync(track.Id, Chart(), new string('1', 64));
        var stored = (await Factory.FindTrackAsync(track.Id))!;
        await Factory.WithServiceAsync<bool, Api.InstrumentViews.TrackAnalysisService>(service => service.RefreshStalenessAsync(stored, AnalysisKind.Chords, Ct));

        var view = await KeyAsync(ApiRoutes.Key(track.Id, 0, null));

        (view.Status, view.Display).ShouldBe((AnalysisStatusDto.Stale, "C major"));
    }

    [Fact]
    public async Task WithoutReadyChords_TheKeyIsNull_AndTheTrackHasNoSummary()
    {
        var track = await Factory.SeedAsync(GridTrack(reliable: true));
        var queued = await Factory.SeedAsync(GridTrack(reliable: true, "bbbbbbbbbbb"));
        await Factory.SeedQueuedAsync(queued.Id, AnalysisKind.Chords, JobId);

        var none = await KeyAsync(ApiRoutes.Key(track.Id, 0, null));
        var running = await KeyAsync(ApiRoutes.Key(queued.Id, 0, null));

        (none.Status, none.Key, none.Display, none.WrittenKey, none.Regions.Count).ShouldBe((AnalysisStatusDto.None, null, null, null, 0));
        (running.Status, running.Key).ShouldBe((AnalysisStatusDto.Queued, null));
        (await Client.GetFromJsonAsync<TrackDto>(ApiRoutes.Track(queued.Id), ApiFactory.Json, Ct))!.Key.ShouldBeNull();
    }

    [Fact]
    public async Task UnknownTracks_Are404()
    {
        using var response = await Client.GetAsync(new Uri(ApiRoutes.Key(Guid.NewGuid(), 0, null), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "getKey");
    }

    [Theory]
    [InlineData("transpose=13", "transpose")]
    [InlineData("transpose=-13", "transpose")]
    [InlineData("instrument=kazoo", "instrument")]
    [InlineData("instrument=HORN-F", "instrument")]
    [InlineData("instrument=99", "instrument")]
    [InlineData("instrument=-1", "instrument")]
    [InlineData("instrument=0", "instrument")]
    [InlineData("transpose=-2147483648", "transpose")]
    public async Task OutOfRangeQueries_Are422(string query, string field)
    {
        var track = await Factory.SeedAsync(GridTrack(reliable: true));

        using var response = await Client.GetAsync(new Uri($"/api/tracks/{track.Id}/key?{query}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "getKey");
        (await response.JsonAsync()).GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
    }
}
