using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Mixer;

/// <summary>
/// Derived practice values of the mixer (data-model.md 3, FR-008): track tempo, effective tempo, quality hint, what is
/// heard now and whether the loop allows the speed trainer. Pure functions; the components only render them.
/// </summary>
public static class PracticeRules
{
    /// <summary>Speed below which the quality hint shows (percent).</summary>
    public const int QualityHintBelowPercent = 70;

    /// <summary>Transpose beyond which the quality hint shows (absolute semitones).</summary>
    public const int QualityHintBeyondSemitones = 7;

    /// <summary>Shortest loop (seconds of the song) for the speed trainer and the count-in before every pass.</summary>
    public const double MinPassSeconds = 0.5;

    /// <summary>Reason shown while no loop region is enabled (ui-test-ids 1.11.0 <c>trainer-needs-loop</c>).</summary>
    public const string NeedsLoopMessage = "Set a loop (A-B) to use the speed trainer.";

    /// <summary>Reason shown while the enabled loop is shorter than <see cref="MinPassSeconds"/> (audio-engine.md "Explicit passes").</summary>
    public const string LoopTooShortMessage = "The loop must be at least 0.5 s long.";

    /// <summary>The track tempo: the manual BPM, else the BPM of a reliable beat grid, else none.</summary>
    /// <param name="metronome">The metronome settings (manual BPM).</param>
    /// <param name="grid">The detected beat grid, if any.</param>
    /// <returns>The tempo in BPM, or null when none is known.</returns>
    public static double? TrackBpm(MetronomeSettingsDto metronome, BeatGridDto? grid) =>
        metronome.BpmOverride ?? (grid is { Reliable: true, Bpm: > 0 } ? grid.Bpm : null);

    /// <summary>The tempo heard now: track tempo x heard speed, rounded.</summary>
    /// <param name="trackBpm">The track tempo, or null.</param>
    /// <param name="speedPercent">The heard speed.</param>
    /// <returns>The rounded BPM, or null without a track tempo.</returns>
    public static int? EffectiveBpm(double? trackBpm, int speedPercent) =>
        trackBpm is { } bpm ? (int)Math.Round(bpm * speedPercent / 100, MidpointRounding.AwayFromZero) : null;

    /// <summary>True below 70 % or beyond +-7 semitones, where time stretching is clearly audible.</summary>
    /// <param name="speedPercent">The heard speed.</param>
    /// <param name="transposeSemitones">The heard transpose.</param>
    /// <returns>Whether the quality hint shows.</returns>
    public static bool NeedsQualityHint(int speedPercent, int transposeSemitones) =>
        speedPercent < QualityHintBelowPercent || Math.Abs(transposeSemitones) > QualityHintBeyondSemitones;

    /// <summary>Neutral = 100 %, 0 semitones and the trainer off (the count-in needs no time stretching).</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>Whether the time-stretch stage is not needed.</returns>
    public static bool IsNeutral(PracticeSettingsDto settings) =>
        settings is { SpeedPercent: 100, TransposeSemitones: 0, Trainer.Enabled: false };

    /// <summary>The speed heard now: 100 while the stage is unavailable, the trainer's speed while it runs, else the setting.</summary>
    /// <param name="settings">The mixer's settings.</param>
    /// <param name="status">The engine's last snapshot.</param>
    /// <returns>The speed in percent.</returns>
    public static int HeardSpeed(PracticeSettingsDto settings, PracticeStatus status) => status switch
    {
        { Stretch: StretchState.Unavailable } => 100,
        _ when TrainerRuns(status) => status.Trainer.SpeedPercent,
        _ => settings.SpeedPercent,
    };

    /// <summary>The transpose heard now: 0 while the stage is unavailable, else the setting.</summary>
    /// <param name="settings">The mixer's settings.</param>
    /// <param name="status">The engine's last snapshot.</param>
    /// <returns>The transpose in semitones.</returns>
    public static int HeardTranspose(PracticeSettingsDto settings, PracticeStatus status) =>
        status.Stretch == StretchState.Unavailable ? 0 : settings.TransposeSemitones;

    /// <summary>True while the trainer plays passes (running or at the target): the speed control is read-only.</summary>
    /// <param name="status">The engine's last snapshot.</param>
    /// <returns>Whether the trainer controls the speed.</returns>
    public static bool TrainerRuns(PracticeStatus status) => status.Trainer.State is TrainerState.Running or TrainerState.Target;

    /// <summary>Why the loop does not allow explicit passes (trainer, count-in every pass), or null when it does.</summary>
    /// <param name="loop">The mixer's loop region.</param>
    /// <returns>The reason, or null.</returns>
    public static string? LoopProblem(LoopRegionDto? loop) => loop switch
    {
        null or { Enabled: false } => NeedsLoopMessage,
        _ when loop.End - loop.Start < MinPassSeconds => LoopTooShortMessage,
        _ => null,
    };
}
