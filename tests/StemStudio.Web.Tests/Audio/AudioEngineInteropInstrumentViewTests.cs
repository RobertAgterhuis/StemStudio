using Bunit;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Tests.Audio;

/// <summary>Engine 2.6.0 calls of the wrapper (016): chord lane, banner chords, hit lanes, singing feedback and the two callbacks.</summary>
public sealed class AudioEngineInteropInstrumentViewTests : IAsyncDisposable
{
    private static readonly ChordChartData Chart = new([new ChordSymbolEvent(0, 500, 0, "maj", null)], 0, "major", []);
    private static readonly PitchContour Melody = new([0, 4500]);
    private static readonly System.Text.Json.JsonSerializerOptions WebJson = new(System.Text.Json.JsonSerializerDefaults.Web);
    private readonly BunitContext _context = new();
    private readonly BunitJSInterop _engine;
    private readonly AudioEngineInterop _interop;

    public AudioEngineInteropInstrumentViewTests()
    {
        _engine = _context.JSInterop.SetupModule("./js/audio-engine.js").SetupModule("createEngine", _ => true);
        _engine.Mode = JSRuntimeMode.Loose;
        _interop = new AudioEngineInterop(_context.JSInterop.JSRuntime, Options.Create(new StemStudioWebOptions()));
    }

    public async ValueTask DisposeAsync()
    {
        await _interop.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task ChordViews_InvokeTheEngine()
    {
        await _interop.InitializeAsync();
        var options = new ChordLaneOptions { ReducedMotion = true };

        await _interop.AttachChordLaneAsync("chord-lane-host", Chart, options);
        await _interop.SetKaraokeChordsAsync(Chart);
        await _interop.SetKaraokeChordsAsync(null);
        await _interop.SetChordDisplayAsync(new ChordDisplay(3, true));
        await _interop.DetachChordLaneAsync();

        _engine.VerifyInvoke("attachChordLane").Arguments.ShouldBe(["chord-lane-host", Chart, options]);
        _engine.Invocations["setKaraokeChords"].Select(i => i.Arguments[0]).ShouldBe([Chart, null]);
        _engine.VerifyInvoke("setChordDisplay").Arguments.ShouldBe([new ChordDisplay(3, true)]);
        _engine.VerifyInvoke("detachChordLane");
    }

    [Fact]
    public async Task HitLanes_InvokeTheEngine()
    {
        await _interop.InitializeAsync();
        DrumHitEvent[] hits = [new(500, "kick", 0.9)];
        var options = new HitLaneOptions { LookAheadMs = 6000 };

        await _interop.AttachHitLanesAsync("drum-lanes-host", hits, options);
        await _interop.SetHitLaneOptionsAsync(options with { LookAheadMs = 2000 });
        await _interop.DetachHitLanesAsync();

        _engine.VerifyInvoke("attachHitLanes").Arguments.ShouldBe(["drum-lanes-host", hits, options]);
        _engine.VerifyInvoke("setHitLaneOptions").Arguments.ShouldBe([options with { LookAheadMs = 2000 }]);
        _engine.VerifyInvoke("detachHitLanes");
    }

    [Fact]
    public async Task Detach_WithoutAnEngine_DoesNothing()
    {
        await Should.NotThrowAsync(() => _interop.DetachChordLaneAsync());
        await Should.NotThrowAsync(() => _interop.DetachHitLanesAsync());
        await Should.NotThrowAsync(() => _interop.SetKaraokeChordsAsync(null));
    }

    [Fact]
    public async Task Singing_StartToleranceStopLatencyAndTake()
    {
        _engine.Setup<SingingSummary>("stopSingingFeedback").SetResult(new SingingSummary { UnitsScored = 2, Average = 87, LatencySource = "loopback" });
        _engine.Setup<RoundTripLatency>("getRoundTripLatency").SetResult(new RoundTripLatency(42, "estimated"));
        _engine.Setup<TakeScoreResult>("scoreTake", _ => true).SetResult(new TakeScoreResult { Average = 70 });
        await _interop.InitializeAsync();
        var request = new SingingFeedbackRequest { PitchViewElementId = "pitch-view-host", Reference = Melody, Units = [new SingingUnit(0, 0, 2000)], ToleranceCents = 50 };
        var take = new TakeScoreRequest { TakeUrl = "http://x/api/takes/1/audio", StartSample = 0, Reference = Melody, Units = request.Units, ToleranceCents = 50 };

        await _interop.StartSingingFeedbackAsync(request);
        await _interop.SetSingingToleranceAsync(75);
        (await _interop.StopSingingFeedbackAsync()).Average.ShouldBe(87);
        (await _interop.GetRoundTripLatencyAsync()).Ms.ShouldBe(42);
        (await _interop.ScoreTakeAsync(take)).Average.ShouldBe(70);

        _engine.VerifyInvoke("startSingingFeedback").Arguments.ShouldBe([request]);
        _engine.VerifyInvoke("setSingingTolerance").Arguments.ShouldBe([75]);
    }

    [Fact]
    public async Task ScoreTake_SendsTheTranspose_AsCamelCaseTransposeSemitones()
    {
        _engine.Setup<TakeScoreResult>("scoreTake", _ => true).SetResult(new TakeScoreResult());
        await _interop.InitializeAsync();
        var take = new TakeScoreRequest
        {
            TakeUrl = "http://x/api/takes/1/audio", StartSample = 0, Reference = Melody, Units = [new SingingUnit(0, 0, 2000)], ToleranceCents = 50,
            TransposeSemitones = -3,
        };

        await _interop.ScoreTakeAsync(take);

        var sent = (TakeScoreRequest)_engine.VerifyInvoke("scoreTake").Arguments[0]!;
        sent.TransposeSemitones.ShouldBe(-3);
        System.Text.Json.JsonSerializer.Serialize(sent, WebJson)
            .ShouldContain("\"transposeSemitones\":-3");
    }

    [Fact]
    public async Task EngineErrors_BecomeAudioEngineExceptions()
    {
        _engine.SetupVoid("attachChordLane", _ => true).SetException(new JSException("Invalid chord chart: segments not ordered"));
        await _interop.InitializeAsync();

        var error = await Should.ThrowAsync<AudioEngineException>(() => _interop.AttachChordLaneAsync("chord-lane-host", Chart, new ChordLaneOptions()));

        error.Message.ShouldContain("Invalid chord chart");
    }

    [Fact]
    public void SingingCallbacks_RaiseTheEvents()
    {
        var scores = new List<SingingUnitScore>();
        var states = new List<SingingStateChange>();
        _interop.SingingUnitScored += (_, s) => scores.Add(s);
        _interop.SingingStateChanged += (_, s) => states.Add(s);

        _interop.OnSingingUnitScored(2, 87, 1);
        _interop.OnSingingUnitScored(3, null, 1);
        _interop.OnSingingStateChanged("failed", "Pitch tracking is too slow on this device.");

        scores.ShouldBe([new SingingUnitScore(2, 87, 1), new SingingUnitScore(3, null, 1)]);
        states.ShouldBe([new SingingStateChange("failed", "Pitch tracking is too slow on this device.")]);
    }

    [Fact]
    public void Methods_AreTheContractNames() =>
        AudioEngineInterop.InstrumentViewMethods.ShouldBe(
        [
            "attachChordLane", "detachChordLane", "setKaraokeChords", "setChordDisplay", "attachHitLanes", "setHitLaneOptions",
            "detachHitLanes", "getRoundTripLatency", "startSingingFeedback", "setSingingTolerance", "stopSingingFeedback", "scoreTake",
        ]);
}
