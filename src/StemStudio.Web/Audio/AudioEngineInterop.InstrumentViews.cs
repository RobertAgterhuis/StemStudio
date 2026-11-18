using Microsoft.JSInterop;

namespace StemStudio.Web.Audio;

/// <summary>Instrument views of the engine wrapper (audio-engine 2.6.0, feature 016).</summary>
public sealed partial class AudioEngineInterop : IInstrumentViewEngine
{
    /// <summary>The 2.6.0 methods this wrapper invokes (checked against <c>types.ts</c>).</summary>
    public static readonly IReadOnlyList<string> InstrumentViewMethods =
    [
        "attachChordLane", "detachChordLane", "setKaraokeChords", "setChordDisplay", "attachHitLanes", "setHitLaneOptions",
        "detachHitLanes", "getRoundTripLatency", "startSingingFeedback", "setSingingTolerance", "stopSingingFeedback", "scoreTake",
    ];

    /// <inheritdoc />
    public event EventHandler<SingingUnitScore>? SingingUnitScored;

    /// <inheritdoc />
    public event EventHandler<SingingStateChange>? SingingStateChanged;

    /// <inheritdoc />
    public Task AttachChordLaneAsync(string elementId, ChordChartData chart, ChordLaneOptions options) =>
        Call("attachChordLane", elementId, chart, options);

    /// <inheritdoc />
    public Task DetachChordLaneAsync() => _engine is null ? Task.CompletedTask : Call("detachChordLane");

    /// <inheritdoc />
    public Task SetKaraokeChordsAsync(ChordChartData? chart) =>
        _engine is null && chart is null ? Task.CompletedTask : Call("setKaraokeChords", chart);

    /// <inheritdoc />
    public Task SetChordDisplayAsync(ChordDisplay display) => Call("setChordDisplay", display);

    /// <inheritdoc />
    public Task AttachHitLanesAsync(string elementId, IReadOnlyList<DrumHitEvent> hits, HitLaneOptions options) =>
        Call("attachHitLanes", elementId, hits, options);

    /// <inheritdoc />
    public Task SetHitLaneOptionsAsync(HitLaneOptions options) => Call("setHitLaneOptions", options);

    /// <inheritdoc />
    public Task DetachHitLanesAsync() => _engine is null ? Task.CompletedTask : Call("detachHitLanes");

    /// <inheritdoc />
    public Task<RoundTripLatency> GetRoundTripLatencyAsync() => Ask<RoundTripLatency>("getRoundTripLatency");

    /// <inheritdoc />
    public Task StartSingingFeedbackAsync(SingingFeedbackRequest request) => Call("startSingingFeedback", request);

    /// <inheritdoc />
    public Task SetSingingToleranceAsync(int cents) => Call("setSingingTolerance", cents);

    /// <inheritdoc />
    public Task<SingingSummary> StopSingingFeedbackAsync() => Ask<SingingSummary>("stopSingingFeedback");

    /// <inheritdoc />
    public Task<TakeScoreResult> ScoreTakeAsync(TakeScoreRequest request) => Ask<TakeScoreResult>("scoreTake", request);

    /// <summary>Engine callback <c>OnSingingUnitScored</c> (2.6.0).</summary>
    /// <param name="index">Unit index.</param>
    /// <param name="score">0..100, or null.</param>
    /// <param name="pass">1-based loop pass.</param>
    [JSInvokable]
    public void OnSingingUnitScored(int index, int? score, int pass) => SingingUnitScored?.Invoke(this, new SingingUnitScore(index, score, pass));

    /// <summary>Engine callback <c>OnSingingStateChanged</c> (2.6.0).</summary>
    /// <param name="state">The <c>SingingState</c>.</param>
    /// <param name="reason">Readable reason of <c>failed</c>, or null.</param>
    [JSInvokable]
    public void OnSingingStateChanged(string state, string? reason) => SingingStateChanged?.Invoke(this, new SingingStateChange(state, reason));

    private Task<T> Ask<T>(string method, params object?[] args)
    {
        var engine = Engine;
        return Guard(() => engine.InvokeAsync<T>(method, args).AsTask());
    }
}
