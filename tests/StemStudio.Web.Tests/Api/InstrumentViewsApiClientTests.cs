using System.Net;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Feature 016 operations of the typed client (T059, backend contract 1.15.0): method, route, body and result.</summary>
public sealed class InstrumentViewsApiClientTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid Track = TestData.TrackId;
    private readonly StubHttpMessageHandler _handler = new();
    private readonly StemStudioApiClient _client;

    public InstrumentViewsApiClientTests() => _client = new StemStudioApiClient(StubHttpMessageHandler.CreateClient(_handler));

    public void Dispose() => _handler.Dispose();

    private CapturedRequest Single(HttpMethod method, string path)
    {
        var request = _handler.Requests.ShouldHaveSingleItem();
        (request.Method, request.PathAndQuery).ShouldBe((method, path));
        _handler.Requests.Clear();
        return request;
    }

    [Fact]
    public async Task Chords_GetDetectEditAndDelete()
    {
        _handler.Respond(HttpStatusCode.OK, InstrumentViewsData.ChordsJson);
        var chords = await _client.GetChordsAsync(Track, Ct);
        (chords.Status, chords.Document!.Segments[1].Quality, chords.Document.Key!.Tonic).ShouldBe((AnalysisStatusDto.Ready, ChordQualityDto.Maj, 0));
        Single(HttpMethod.Get, $"/api/tracks/{Track}/chords");

        _handler.Respond(HttpStatusCode.Accepted, InstrumentViewsData.ToJson(InstrumentViewsData.Chords(AnalysisStatusDto.Queued)));
        var queued = await _client.DetectChordsAsync(Track, new DetectChordsRequest { Vocabulary = ChordVocabularyDto.Full, OverwriteEdits = true }, Ct);
        queued.Status.ShouldBe(AnalysisStatusDto.Queued);
        var body = Single(HttpMethod.Post, $"/api/tracks/{Track}/chords").Body!;
        body.ShouldContain("\"vocabulary\":\"full\"");
        body.ShouldContain("\"overwriteEdits\":true");

        _handler.Respond(HttpStatusCode.OK, InstrumentViewsData.ChordsJson);
        await _client.EditChordSegmentAsync(Track, 1, new EditChordRequest { Label = "A:min7/b7" }, Ct);
        Single(HttpMethod.Put, $"/api/tracks/{Track}/chords/segments/1").Body.ShouldBe("""{"label":"A:min7/b7"}""");

        _handler.Respond(HttpStatusCode.NoContent);
        await _client.DeleteChordsAsync(Track, Ct);
        Single(HttpMethod.Delete, $"/api/tracks/{Track}/chords");
    }

    [Fact]
    public async Task Chords_EditsExist_ThrowsWithTheProblemCode()
    {
        _handler.RespondProblem(HttpStatusCode.Conflict, """{"type":"https://stemstudio.local/problems/edits-exist","title":"Edited","status":409}""");

        var error = (await Should.ThrowAsync<ApiException>(() => _client.DetectChordsAsync(Track, new DetectChordsRequest(), Ct))).Error;

        (error.IsConflict, error.Code).ShouldBe((true, "edits-exist"));
    }

    [Fact]
    public async Task SheetKeyAndCapo_SendTheDisplayQuery()
    {
        _handler.Respond(HttpStatusCode.OK, InstrumentViewsData.SheetJson);
        (await _client.GetChordSheetAsync(Track, -2, 3, Ct)).Rows[0].Chords[1].Symbol.ShouldBe("E");
        Single(HttpMethod.Get, $"/api/tracks/{Track}/chords/sheet?transpose=-2&capo=3");

        _handler.Respond(HttpStatusCode.OK, InstrumentViewsData.KeyJson);
        (await _client.GetKeyAsync(Track, 2, WrittenInstrumentDto.AltoSaxEb, Ct)).Regions[1].StartBar.ShouldBe(9);
        Single(HttpMethod.Get, $"/api/tracks/{Track}/key?transpose=2&instrument=alto-sax-eb");

        _handler.Respond(HttpStatusCode.OK, InstrumentViewsData.KeyJson);
        (await _client.GetKeyAsync(Track, 0, null, Ct)).Display.ShouldBe("D major (original C major, +2)");
        Single(HttpMethod.Get, $"/api/tracks/{Track}/key?transpose=0");

        _handler.Respond(HttpStatusCode.OK, InstrumentViewsData.CapoJson);
        (await _client.GetCapoSuggestionsAsync(Track, 3, Ct)).Suggestions.Select(s => s.Capo).ShouldBe([3, 1, 6]);
        Single(HttpMethod.Get, $"/api/tracks/{Track}/chords/capo-suggestions?transpose=3");
    }

    [Fact]
    public async Task DrumHits_GetDetectAndDelete()
    {
        _handler.Respond(HttpStatusCode.OK, InstrumentViewsData.ToJson(InstrumentViewsData.DrumHits()));
        (await _client.GetDrumHitsAsync(Track, Ct)).Document!.Hits.Count.ShouldBe(3);
        Single(HttpMethod.Get, $"/api/tracks/{Track}/drum-hits");

        _handler.Respond(HttpStatusCode.Accepted, InstrumentViewsData.ToJson(InstrumentViewsData.DrumHits(AnalysisStatusDto.Queued)));
        (await _client.DetectDrumHitsAsync(Track, Ct)).Document.ShouldBeNull();
        Single(HttpMethod.Post, $"/api/tracks/{Track}/drum-hits").Body.ShouldBeNull();

        _handler.Respond(HttpStatusCode.NoContent);
        await _client.DeleteDrumHitsAsync(Track, Ct);
        Single(HttpMethod.Delete, $"/api/tracks/{Track}/drum-hits");
    }

    [Fact]
    public async Task VocalPitch_GetAnalyseAndDelete()
    {
        _handler.Respond(HttpStatusCode.OK, InstrumentViewsData.ToJson(InstrumentViewsData.VocalPitch()));
        (await _client.GetVocalPitchAsync(Track, Ct)).Document!.Source.ShouldBe(VocalSourceDto.VocalsLead);
        Single(HttpMethod.Get, $"/api/tracks/{Track}/vocal-pitch");

        _handler.Respond(HttpStatusCode.Accepted, InstrumentViewsData.ToJson(InstrumentViewsData.VocalPitch(AnalysisStatusDto.Queued)));
        (await _client.AnalyseVocalPitchAsync(Track, Ct)).EstimateSeconds.ShouldBe(8);
        Single(HttpMethod.Post, $"/api/tracks/{Track}/vocal-pitch");

        _handler.Respond(HttpStatusCode.NoContent);
        await _client.DeleteVocalPitchAsync(Track, Ct);
        Single(HttpMethod.Delete, $"/api/tracks/{Track}/vocal-pitch");
    }

    [Fact]
    public async Task Catalogue_AndChannelScores()
    {
        _handler.Respond(HttpStatusCode.OK, InstrumentViewsData.ToJson(InstrumentViewsData.Catalogue()));
        (await _client.GetInstrumentCatalogueAsync(Ct)).Families.Count.ShouldBe(6);
        Single(HttpMethod.Get, "/api/instruments/catalogue");

        _handler.Respond(HttpStatusCode.OK, """{"items":[],"total":0,"totalSizeBytes":0}""");
        (await _client.ListChannelScoresAsync(Track, "drums.kick", Ct)).Total.ShouldBe(0);
        Single(HttpMethod.Get, $"/api/assets?kind=score&trackId={Track}&channel=drums.kick");
    }

    [Fact]
    public async Task Scores_CarryTheTablaturePartAndChordExtensions()
    {
        _handler.Respond(HttpStatusCode.OK, InstrumentViewsData.ToJson(InstrumentViewsData.ScoreOptions()));
        var options = await _client.GetScoreOptionsAsync(AudioSourceDto.FromChannel(Track, StemNames.Guitar), Ct);
        (options.Tablature!.CapoAllowed, options.Parts!.ClefAllowed, options.Chords!.Available).ShouldBe((true, true, true));
        _handler.Requests.Clear();

        _handler.Respond(HttpStatusCode.Accepted, InstrumentViewsData.ToJson(ScoreData.Asset(AssetStatusDto.Queued)));
        var request = new CreateScoreRequest
        {
            Source = AudioSourceDto.FromChannel(Track, StemNames.Guitar),
            Tablature = new ScoreTablatureDto { Tuning = TuningIdDto.GuitarDropD, Capo = 3, Layout = TabLayoutDto.TabOnly },
            Part = new ScorePartDto { Instrument = WrittenInstrumentDto.AltoSaxEb, Clef = ClefChoiceDto.Treble },
            IncludeChords = true,
        };
        (await _client.CreateScoreAsync(request, Ct)).Status.ShouldBe(AssetStatusDto.Queued);
        var body = _handler.Requests.ShouldHaveSingleItem().Body!;
        body.ShouldContain("\"tablature\":{\"tuning\":\"guitar-drop-d\",\"capo\":3,\"layout\":\"tab-only\"}");
        body.ShouldContain("\"part\":{\"instrument\":\"alto-sax-eb\",\"clef\":\"treble\"}");
        body.ShouldContain("\"includeChords\":true");
    }
}
