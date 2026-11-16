using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Mixer;

/// <summary>Stub.</summary>
public static class PracticeRules
{
    /// <summary>Stub.</summary>
    public const string NeedsLoopMessage = "";

    /// <summary>Stub.</summary>
    public const string LoopTooShortMessage = "-";

    /// <summary>Stub.</summary>
    /// <param name="metronome">M.</param>
    /// <param name="grid">G.</param>
    /// <returns>R.</returns>
    public static double? TrackBpm(MetronomeSettingsDto metronome, BeatGridDto? grid) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="trackBpm">B.</param>
    /// <param name="speedPercent">S.</param>
    /// <returns>R.</returns>
    public static int? EffectiveBpm(double? trackBpm, int speedPercent) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="speedPercent">S.</param>
    /// <param name="transposeSemitones">T.</param>
    /// <returns>R.</returns>
    public static bool NeedsQualityHint(int speedPercent, int transposeSemitones) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="settings">S.</param>
    /// <returns>R.</returns>
    public static bool IsNeutral(PracticeSettingsDto settings) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="settings">S.</param>
    /// <param name="status">T.</param>
    /// <returns>R.</returns>
    public static int HeardSpeed(PracticeSettingsDto settings, PracticeStatus status) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="settings">S.</param>
    /// <param name="status">T.</param>
    /// <returns>R.</returns>
    public static int HeardTranspose(PracticeSettingsDto settings, PracticeStatus status) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="status">T.</param>
    /// <returns>R.</returns>
    public static bool TrainerRuns(PracticeStatus status) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="loop">L.</param>
    /// <returns>R.</returns>
    public static string? LoopProblem(LoopRegionDto? loop) => throw new NotImplementedException();
}
