using System.Net;
using System.Text.Json.Nodes;
using StemStudio.Api.InstrumentViews;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Worker;

/// <summary>Feature 016 T049: the instrument-analysis calls of the worker client against the worker 1.9.0 examples.</summary>
public sealed class WorkerAnalysisClientTests
{
    private const string ChordsJobId = "4f1c2b3a4d5e6f708192a3b4c5d6e7f8";
    private const string Track = "tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> RequestExamples() => ["analysis-chords-request", "analysis-drum-hits-request", "analysis-vocal-pitch-request"];

    internal static WorkerAnalysisRequest RequestOf(string example) => example switch
    {
        "analysis-chords-request" => new WorkerChordsRequest
        {
            Inputs = [$"{Track}/stems/bass.flac", $"{Track}/stems/guitar.flac", $"{Track}/stems/piano.flac", $"{Track}/stems/other.flac"],
            BassInput = $"{Track}/stems/bass.flac",
            Grid = new WorkerBeatGridTempo(120, 4, [0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0], [0.5, 2.5]),
            Vocabulary = "simple",
            Engine = null,
        },
        "analysis-drum-hits-request" => new WorkerDrumHitsRequest
        {
            DrumInputs = new WorkerDrumInputs($"{Track}/detail/drums/kick.flac", $"{Track}/detail/drums/snare.flac", $"{Track}/detail/drums/toms.flac", $"{Track}/detail/drums/hihat.flac", $"{Track}/detail/drums/cymbals.flac"),
        },
        _ => new WorkerVocalPitchRequest { Input = $"{Track}/detail/vocals/lead.flac", Source = "vocals.lead" },
    };

    [Theory]
    [MemberData(nameof(RequestExamples))]
    public async Task Create_PostsExactlyTheExampleRequest(string example)
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "analysis-running-chords-job"));

        var job = await client.CreateAnalysisJobAsync(RequestOf(example), Ct);

        (job.AnalysisJobId, job.Status, job.Progress, job.Stage).ShouldBe((ChordsJobId, WorkerPracticeJobState.Running, 0.42, "analysing"));
        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/instrument-analysis-jobs"));
        JsonNode.DeepEquals(JsonNode.Parse(sent.BodyText), JsonNode.Parse(WorkerExamples.Read(example))).ShouldBeTrue(sent.BodyText);
    }

    [Fact]
    public async Task Get_ParsesTheCompletedChordChart_AndTheDevice()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "analysis-completed-chords-job"));

        var job = (await client.GetAnalysisJobAsync(ChordsJobId, Ct))!;

        (job.Kind, job.Status, job.Device).ShouldBe(("chords", WorkerPracticeJobState.Completed, new WorkerJobDevice(ProcessingDeviceKind.Cpu, null, false, null)));
        var chart = AnalysisResultMapper.ToDocument(AnalysisKind.Chords, job.Result!).ShouldBeOfType<ChordChart>();
        (chart.Engine, chart.Vocabulary, chart.PreferFlats, chart.BeatSnapped, chart.NoChords).ShouldBe(("btc", ChordVocabulary.Simple, false, true, false));
        chart.Key.ShouldBe(new SongKey(0, KeyMode.Major, 0.81, null));
        chart.KeyRegions.Count.ShouldBe(2);
        chart.Segments.Select(segment => (segment.Label, segment.Root, segment.Quality)).ShouldBe([("N", (int?)null, (ChordQuality?)null), ("C:maj", 0, ChordQuality.Maj), ("G:maj", 7, ChordQuality.Maj)]);
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/instrument-analysis-jobs/{ChordsJobId}");
    }

    [Fact]
    public async Task Get_ParsesTheFailedJob()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "analysis-failed-pitch-job"));

        var job = (await client.GetAnalysisJobAsync("9a8b7c6d5e4f30211203f4e5d6c7b8a9", Ct))!;

        (job.Kind, job.Status, job.Error, job.Device, job.Result).ShouldBe(("vocal-pitch", WorkerPracticeJobState.Failed, "The vocal stem is silent, so no melody can be shown.", null, null));
    }

    [Fact]
    public async Task Get_UnknownJob_IsNull_AndDeleteTolerates404()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.NotFound, "problem-404"));

        (await client.GetAnalysisJobAsync(ChordsJobId, Ct)).ShouldBeNull();
        await client.DeleteAnalysisJobAsync(ChordsJobId, Ct);

        handler.Requests.Last().Method.ShouldBe(HttpMethod.Delete);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "problem-400-input-path")]
    [InlineData(HttpStatusCode.NotFound, "problem-404-input")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "problem-422-validation")]
    public async Task Problems_KeepTheirStatus(HttpStatusCode status, string example)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(status, example));

        (await Should.ThrowAsync<WorkerRequestException>(() => client.CreateAnalysisJobAsync(RequestOf("analysis-chords-request"), Ct))).StatusCode.ShouldBe(status);
    }

    [Fact]
    public async Task UnreachableWorker_IsUnavailable_AndBadJobIdsAreRefused()
    {
        var (client, handler) = Create(_ => throw new HttpRequestException("connection refused"));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.CreateAnalysisJobAsync(RequestOf("analysis-vocal-pitch-request"), Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.GetAnalysisJobAsync("../../etc/passwd", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.DeleteAnalysisJobAsync("ABC", Ct));
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Health_ParsesTheInstrumentAnalysisLane()
    {
        var health = """
            {"status": "ok", "separator": "fake", "device": "cpu", "deviceInfo": {"kind": "cpu"}, "contractVersion": "1.9.0",
             "instrumentAnalysis": {"models": {"btc-majmin": "ready", "btc-large-voca": "not-downloaded"}, "chordEngine": "btc", "pitchEngine": "pyin"}}
            """;
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, health));

        var lane = (await client.GetHealthAsync(Ct)).InstrumentAnalysis!;

        (lane.ChordEngine, lane.PitchEngine, lane.Models["btc-large-voca"]).ShouldBe(("btc", "pyin", "not-downloaded"));
    }

    [Fact]
    public void Mapper_ReadsDrumHitsAndVocalPitch()
    {
        var hits = AnalysisResultMapper.ToDocument(AnalysisKind.DrumHits, FakeWorkerAnalysisClient.DrumHits()).ShouldBeOfType<DrumHitsDocument>();
        hits.Pieces.ShouldBe([DrumPiece.Kick, DrumPiece.Snare]);
        hits.Hits.ShouldBe([new DrumHit(500, DrumPiece.Kick, 0.9), new DrumHit(1000, DrumPiece.Snare, 0.6)]);

        var pitch = AnalysisResultMapper.ToDocument(AnalysisKind.VocalPitch, FakeWorkerAnalysisClient.VocalPitch("vocals")).ShouldBeOfType<VocalPitchDocument>();
        (pitch.Source, pitch.Engine, pitch.VoicedRatio, pitch.Range).ShouldBe((VocalSource.Vocals, "pyin", 0.5, new PitchRange(5700, 5710)));
        pitch.Frames.ShouldBe([0, 5700, 5710, 0]);
    }

    [Theory]
    [InlineData("chords", """{"kind": "drum-hits", "engine": "drum-onsets", "pieces": [], "hits": []}""")]
    [InlineData("chords", """{"kind": "chords", "engine": "evil", "segments": []}""")]
    [InlineData("chords", """{"kind": "chords", "engine": "btc", "segments": [{"startMs": 0, "endMs": 10, "label": "C:maj; DROP", "root": 0, "quality": "maj", "bass": null, "confidence": 1}]}""")]
    [InlineData("chords", """{"kind": "chords", "engine": "btc", "segments": [{"startMs": 0, "endMs": 10, "label": "C:maj", "root": 12, "quality": "maj", "bass": null, "confidence": 1}]}""")]
    [InlineData("chords", """{"kind": "chords", "engine": "btc", "segments": [{"startMs": 10, "endMs": 10, "label": "C:maj", "root": 0, "quality": "maj", "bass": null, "confidence": 1}]}""")]
    [InlineData("chords", """{"kind": "chords", "engine": "btc", "segments": [{"startMs": 0, "endMs": 10, "label": "C:maj", "root": 0, "quality": "power", "bass": null, "confidence": 1}]}""")]
    [InlineData("chords", """{"kind": "chords", "engine": "btc", "key": {"tonic": 0, "mode": "dorian", "confidence": 1}, "segments": []}""")]
    [InlineData("chords", """{"kind": "chords", "engine": "btc", "segments": [{"startMs": 0, "endMs": 10, "label": "C:maj", "root": 0, "quality": "maj", "bass": null, "confidence": 1.5}]}""")]
    [InlineData("drumHits", """{"kind": "drum-hits", "engine": "drum-onsets", "pieces": ["cowbell"], "hits": []}""")]
    [InlineData("drumHits", """{"kind": "drum-hits", "engine": "drum-onsets", "pieces": [], "hits": [{"timeMs": -1, "piece": "kick", "strength": 1}]}""")]
    [InlineData("vocalPitch", """{"kind": "vocal-pitch", "hopMs": 10, "frames": [12701], "source": "vocals", "engine": "pyin", "voicedRatio": 1, "range": null, "warnings": []}""")]
    [InlineData("vocalPitch", """{"kind": "vocal-pitch", "hopMs": 20, "frames": [], "source": "vocals", "engine": "pyin", "voicedRatio": 1, "range": null, "warnings": []}""")]
    [InlineData("vocalPitch", """{"kind": "vocal-pitch", "hopMs": 10, "frames": [], "source": "drums", "engine": "pyin", "voicedRatio": 1, "range": null, "warnings": []}""")]
    public void Mapper_RefusesResultsOutsideTheContract(string kind, string json) =>
        AnalysisResultMapper.ToDocument(Enum.Parse<AnalysisKind>(kind, ignoreCase: true), FakeWorkerAnalysisClient.Result(json)).ShouldBeNull();

    [Fact]
    public void Mapper_RefusesTooManySegmentsHitsAndFrames()
    {
        var segment = """{"startMs": 0, "endMs": 10, "label": "N", "root": null, "quality": null, "bass": null, "confidence": 1}""";
        var chords = $$"""{"kind": "chords", "engine": "btc", "segments": [{{string.Join(',', Enumerable.Repeat(segment, 4001))}}]}""";
        var frames = $$"""{"kind": "vocal-pitch", "hopMs": 10, "frames": [{{string.Join(',', Enumerable.Repeat("0", 90_001))}}], "source": "vocals", "engine": "pyin", "voicedRatio": 0, "range": null, "warnings": []}""";

        AnalysisResultMapper.ToDocument(AnalysisKind.Chords, FakeWorkerAnalysisClient.Result(chords)).ShouldBeNull();
        AnalysisResultMapper.ToDocument(AnalysisKind.VocalPitch, FakeWorkerAnalysisClient.Result(frames)).ShouldBeNull();
    }

    [Fact]
    public void Mapper_KeepsKnownWarningsAndDropsUnknownOnes()
    {
        var json = """
            {"kind": "chords", "engine": "chroma-hmm", "vocabulary": "simple", "key": null, "keyRegions": [], "noChords": true, "segments": [],
             "warnings": [{"code": "vocabulary-reduced", "text": "The fast method knows only major and minor chords."}, {"code": "new-thing", "text": "x"}]}
            """;

        var chart = AnalysisResultMapper.ToDocument(AnalysisKind.Chords, FakeWorkerAnalysisClient.Result(json)).ShouldBeOfType<ChordChart>();

        chart.Warnings.ShouldBe([new AnalysisWarning(AnalysisWarningCode.VocabularyReduced, "The fast method knows only major and minor chords.")]);
        (chart.NoChords, chart.Key).ShouldBe((true, null));
    }

    [Theory]
    [InlineData(AnalysisKind.Chords, "chords")]
    [InlineData(AnalysisKind.DrumHits, "drum-hits")]
    [InlineData(AnalysisKind.VocalPitch, "vocal-pitch")]
    public void WireKind_IsTheWorkerName(AnalysisKind kind, string wire) => AnalysisResultMapper.WireKind(kind).ShouldBe(wire);

    private static (WorkerClient Client, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        WorkerClientTests.Create(respond);
}
