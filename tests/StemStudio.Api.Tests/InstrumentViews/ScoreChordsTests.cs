using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StemStudio.Api.Scores;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Scores;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using static StemStudio.Api.Tests.InstrumentViews.AnalysisTestData;

namespace StemStudio.Api.Tests.InstrumentViews;

/// <summary>Feature 016 T072: chord symbols in sheet music (FR-017): <c>includeChords</c>, the options and the facts.</summary>
public sealed class ScoreChordsTests : ApiTestBase
{
    private static ChordChart FlatChart() => Chart() with
    {
        PreferFlats = true,
        Segments =
        [
            new ChordSegment { StartMs = 0, EndMs = 3000, Label = "N", Confidence = 0.9 },
            new ChordSegment { StartMs = 3000, EndMs = 6000, Label = "A:min7/b7", Root = 9, Quality = ChordQuality.Min7, Bass = 7, Confidence = 0.9 },
            new ChordSegment { StartMs = 6000, EndMs = 12000, Label = "Bb:maj", Root = 10, Quality = ChordQuality.Maj, Confidence = 0.9 },
            new ChordSegment { StartMs = 12000, EndMs = 20000, Label = "F:7", Root = 5, Quality = ChordQuality.Seventh, Confidence = 0.9 },
        ],
    };

    [Fact]
    public async Task IncludeChords_SendsTheSymbolsShiftedToTheRangeStart_AndThePreferFlatsHint()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        await Factory.SeedReadyAsync(track.Id, FlatChart(), new string('1', 64));

        using var response = await Client.PostScoreAsync(ScoreTestData.Request(track.Id, "guitar") with { IncludeChords = true, Range = new RangeDto(5, 15) });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var sent = Worker.TranscriptionCreates.Single();
        sent.Chords.ShouldBe([new WorkerScoreChord(0, 9, "min7", 7), new WorkerScoreChord(1000, 10, "maj", null), new WorkerScoreChord(7000, 5, "7", null)]);
        sent.PreferFlats.ShouldBe(true);
        var asset = await response.BodyAsync<AssetDto>();
        asset.Provenance.ScoreRecipe!.IncludeChords.ShouldBeTrue();
    }

    [Fact]
    public async Task WithoutIncludeChords_TheRequestKeepsThe008Shape()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        await Factory.SeedReadyAsync(track.Id, FlatChart(), new string('1', 64));

        using var response = await Client.PostScoreAsync(ScoreTestData.Request(track.Id, "bass"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var json = JsonSerializer.Serialize(Worker.TranscriptionCreates.Single(), ApiFactory.Json);
        foreach (var name in new[] { "chords", "preferFlats", "tablature", "part", "singlePiece" })
        {
            json.ShouldNotContain($"\"{name}\"");
        }
    }

    [Fact]
    public async Task IncludeChords_WithoutReadyChords_Is409ChordsNotReady()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.PostScoreAsync(ScoreTestData.Request(track.Id, "vocals") with { IncludeChords = true });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "createScore");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"type\":\"chords-not-ready\"");
        Worker.TranscriptionCreates.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ScoreProfileDto.Bass)]
    [InlineData(ScoreProfileDto.Drums)]
    public async Task IncludeChords_ForABassOrDrumScore_Is422(ScoreProfileDto profile)
    {
        var track = await Factory.SeedAsync(ScoreTestData.DrumsSplitTrack());
        await Factory.SeedReadyAsync(track.Id, FlatChart(), new string('1', 64));
        var channel = profile == ScoreProfileDto.Drums ? "drums" : "bass";

        using var response = await Client.PostScoreAsync(ScoreTestData.Request(track.Id, channel) with { Profile = profile, IncludeChords = true });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("includeChords");
    }

    [Fact]
    public async Task Options_OfferChordsOnlyWhenTheChordsAreReady()
    {
        var track = await Factory.SeedAsync(ScoreTestData.DrumsSplitTrack());

        var before = await OptionsAsync(ApiRoutes.ScoreOptionsForChannel(track.Id, "guitar"));
        await Factory.SeedReadyAsync(track.Id, FlatChart(), new string('1', 64));
        var guitar = await OptionsAsync(ApiRoutes.ScoreOptionsForChannel(track.Id, "guitar"));
        var drums = await OptionsAsync(ApiRoutes.ScoreOptionsForChannel(track.Id, "drums"));

        (before.Chords!.Available, guitar.Chords!.Available, drums.Chords!.Available).ShouldBe((false, true, false));
    }

    [Fact]
    public async Task TheFacts_ReportChordsIncluded_AndARetryKeepsTheChords()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        await Factory.SeedReadyAsync(track.Id, FlatChart(), new string('1', 64));
        using var created = await Client.PostScoreAsync(ScoreTestData.Request(track.Id, "piano") with { Profile = ScoreProfileDto.Piano, IncludeChords = true });
        var asset = await created.BodyAsync<AssetDto>();
        ScoreTestData.CompleteTranscriptions(Worker, ScoreTestData.Result(ScoreTestData.Summary(ScoreProfile.Piano) with { ChordsIncluded = true }));

        await Factory.SyncAssetsAsync();

        var ready = await Client.GetFromJsonAsync<AssetDto>(ApiRoutes.Asset(asset.Id), ApiFactory.Json, Ct);
        ready!.Score!.ChordsIncluded.ShouldBeTrue();
    }

    [Fact]
    public void Wire_MapsTheNotationAdditions()
    {
        ScoreNotationWire.ToWire(new TablatureSettings(TuningId.Bass5Standard, 0, TabLayout.TabOnly)).ShouldBe(new WorkerTablatureSettings("bass-5-standard", 0, "tab-only"));
        ScoreNotationWire.ToWire(new PartSettings(WrittenInstrument.AltoSaxEb, ClefChoice.Treble8vb)).ShouldBe(new WorkerPartSettings("alto-sax-eb", "treble-8vb"));
        ScoreNotationWire.ToWire((TablatureSettings?)null).ShouldBeNull();
        ScoreNotationWire.ToWire((PartSettings?)null).ShouldBeNull();
        ScoreNotationWire.Chords(null).ShouldBeNull();
        ScoreNotationWire.FromWire<Contracts.InstrumentViews.TuningIdDto>("guitar-7-string").ShouldBeNull();
        ScoreNotationWire.FromWire<Contracts.InstrumentViews.TuningIdDto>(null).ShouldBeNull();
        var tab = ScoreNotationWire.ToFacts(new WorkerTablatureSummary { Tuning = "guitar-drop-d", Strings = 6, Capo = 2, Layout = "notation-and-tab", OctaveMoved = 1, NotesDropped = 2, WideStretches = 3 });
        tab.ShouldBe(new TablatureFacts { Tuning = TuningId.GuitarDropD, Strings = 6, Capo = 2, Layout = TabLayout.NotationAndTab, OctaveMoved = 1, NotesDropped = 2, WideStretches = 3 });
        ScoreNotationWire.ToFacts(new WorkerTablatureSummary { Tuning = "lute", Strings = 6, Layout = "tab-only" }).ShouldBeNull();
        var part = ScoreNotationWire.ToFacts(new WorkerPartSummary { Instrument = "horn-f", Name = "Horn in F", Interval = "P5", WrittenKey = "G", Clef = "treble", OutOfRange = 1 });
        ScoreNotationWire.ToDto(part)!.ShouldSatisfyAllConditions(dto => dto.Clef.ShouldBe("treble"), dto => dto.Instrument.ShouldBe(Contracts.InstrumentViews.WrittenInstrumentDto.HornF));
        ScoreNotationWire.ToFacts(new WorkerPartSummary { Instrument = "kazoo", Name = "x", Interval = "P1", Clef = "treble" }).ShouldBeNull();
        ScoreNotationWire.ToDto(tab)!.Tuning.ShouldBe(Contracts.InstrumentViews.TuningIdDto.GuitarDropD);
        (ScoreNotationWire.ToDto((TablatureFacts?)null), ScoreNotationWire.ToDto((PartFacts?)null)).ShouldBe((null, null));
    }

    private async Task<ScoreOptionsDto> OptionsAsync(string route)
    {
        using var response = await Client.GetAsync(new Uri(route, UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(response, "getScoreOptions");
        return await response.BodyAsync<ScoreOptionsDto>();
    }
}
