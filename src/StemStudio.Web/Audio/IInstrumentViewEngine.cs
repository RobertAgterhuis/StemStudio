namespace StemStudio.Web.Audio;

/// <summary>
/// Instrument views of the audio engine (contract 2.6.0, feature 016): chord lane and banner chords, drum hit lanes and
/// singing feedback. Implemented by <see cref="AudioEngineInterop"/> on the same engine object as <see cref="IAudioEngine"/>;
/// every call needs the engine to be initialized, except the detach calls, which do nothing without one.
/// </summary>
public interface IInstrumentViewEngine
{
    /// <summary>Raised once per finished singing unit (<c>OnSingingUnitScored</c>).</summary>
    event EventHandler<SingingUnitScore>? SingingUnitScored;

    /// <summary>Raised on every singing state change (<c>OnSingingStateChanged</c>).</summary>
    event EventHandler<SingingStateChange>? SingingStateChanged;

    /// <summary><c>attachChordLane</c>: engine-owned chord lane in the host.</summary>
    /// <param name="elementId">Host element id.</param>
    /// <param name="chart">The chart.</param>
    /// <param name="options">Lane options.</param>
    /// <returns>A task.</returns>
    Task AttachChordLaneAsync(string elementId, ChordChartData chart, ChordLaneOptions options);

    /// <summary><c>detachChordLane</c>.</summary>
    /// <returns>A task.</returns>
    Task DetachChordLaneAsync();

    /// <summary><c>setKaraokeChords</c>: chords above the karaoke words, or none.</summary>
    /// <param name="chart">The chart, or null to remove them.</param>
    /// <returns>A task.</returns>
    Task SetKaraokeChordsAsync(ChordChartData? chart);

    /// <summary><c>setChordDisplay</c>: capo and sounding symbols for every chord view.</summary>
    /// <param name="display">The display.</param>
    /// <returns>A task.</returns>
    Task SetChordDisplayAsync(ChordDisplay display);

    /// <summary><c>attachHitLanes</c>: drum hit lanes in the host.</summary>
    /// <param name="elementId">Host element id.</param>
    /// <param name="hits">The hits in time order.</param>
    /// <param name="options">Lane options.</param>
    /// <returns>A task.</returns>
    Task AttachHitLanesAsync(string elementId, IReadOnlyList<DrumHitEvent> hits, HitLaneOptions options);

    /// <summary><c>setHitLaneOptions</c>.</summary>
    /// <param name="options">Look-ahead or pieces.</param>
    /// <returns>A task.</returns>
    Task SetHitLaneOptionsAsync(HitLaneOptions options);

    /// <summary><c>detachHitLanes</c>.</summary>
    /// <returns>A task.</returns>
    Task DetachHitLanesAsync();

    /// <summary><c>getRoundTripLatency</c>.</summary>
    /// <returns>The latency singing feedback would use.</returns>
    Task<RoundTripLatency> GetRoundTripLatencyAsync();

    /// <summary><c>startSingingFeedback</c>.</summary>
    /// <param name="request">Pitch view host, reference, units, tolerance and the 015 calibration.</param>
    /// <returns>A task that completes when feedback runs.</returns>
    Task StartSingingFeedbackAsync(SingingFeedbackRequest request);

    /// <summary><c>setSingingTolerance</c>.</summary>
    /// <param name="cents">10..200.</param>
    /// <returns>A task.</returns>
    Task SetSingingToleranceAsync(int cents);

    /// <summary><c>stopSingingFeedback</c>.</summary>
    /// <returns>The summary.</returns>
    Task<SingingSummary> StopSingingFeedbackAsync();

    /// <summary><c>scoreTake</c>: scores a recorded 015 take offline.</summary>
    /// <param name="request">The take and the reference.</param>
    /// <returns>The take's scores.</returns>
    Task<TakeScoreResult> ScoreTakeAsync(TakeScoreRequest request);
}
