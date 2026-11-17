namespace StemStudio.Domain.Recording;

/// <summary>Why a take cannot be stored (data-model 2.1 "Rules"); each maps to a problem code.</summary>
public enum TakeRefusal
{
    /// <summary>The take may be stored.</summary>
    None,

    /// <summary>409 <c>take-limit-reached</c>.</summary>
    LimitReached,

    /// <summary>422 <c>recording-speed</c>.</summary>
    Speed,

    /// <summary>422 <c>length-mismatch</c>.</summary>
    LengthMismatch,

    /// <summary>422 <c>take-outside-track</c>.</summary>
    OutsideTrack,
}

/// <summary>Placement, limit and speed rules of takes (data-model 2.1).</summary>
public static class TakeRules
{
    /// <summary>True when another take may be added to a track with <paramref name="existing"/> takes.</summary>
    /// <param name="existing">Takes of the track.</param>
    /// <returns>Whether one more fits.</returns>
    public static bool CanAdd(int existing) => existing < RecordingConstants.MaxTakesPerTrack;

    /// <summary>Only 100 % speed and no transpose may be recorded.</summary>
    /// <param name="speed">Playback speed.</param>
    /// <param name="transposeSemitones">Transpose.</param>
    /// <returns>Whether the take is allowed.</returns>
    public static bool IsSupportedSpeed(double speed, int transposeSemitones) => speed.Equals(1.0) && transposeSemitones == 0;

    /// <summary>True when <c>[start, start + length)</c> lies inside the track (end tolerance 4,410 samples).</summary>
    /// <param name="startSample">First song sample.</param>
    /// <param name="lengthSamples">Length.</param>
    /// <param name="trackSamples">Track length in samples.</param>
    /// <returns>Whether the take lies inside.</returns>
    public static bool IsInsideTrack(long startSample, long lengthSamples, long trackSamples) =>
        startSample >= 0 && lengthSamples > 0 && startSample < trackSamples
        && lengthSamples <= trackSamples + RecordingConstants.OutsideTrackTolerance - startSample;

    /// <summary>The field errors of a take's position and length (bounds of the contract).</summary>
    /// <param name="startSample">First song sample.</param>
    /// <param name="lengthSamples">Length.</param>
    /// <returns>Errors; empty when valid.</returns>
    public static IReadOnlyList<DomainError> SpanErrors(long startSample, long lengthSamples)
    {
        var errors = new ValidationErrors();
        errors.Check(startSample >= 0, "startSample", "Must be 0 or more.");
        errors.Check(
            lengthSamples is >= RecordingConstants.MinTakeSamples and <= RecordingConstants.MaxTakeSamples,
            "lengthSamples",
            $"Must be between {RecordingConstants.MinTakeSamples} and {RecordingConstants.MaxTakeSamples} samples.");
        return errors.Errors;
    }

    /// <summary>The first refusal of an upload whose WAV and metadata are well-formed, in the order of data-model 2.1.</summary>
    /// <param name="check">The values to check.</param>
    /// <returns>The refusal, or <see cref="TakeRefusal.None"/>.</returns>
    public static TakeRefusal Check(TakeCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        if (!CanAdd(check.ExistingTakes))
        {
            return TakeRefusal.LimitReached;
        }

        if (!IsSupportedSpeed(check.Speed, check.TransposeSemitones))
        {
            return TakeRefusal.Speed;
        }

        if (check.WavFrames != check.LengthSamples)
        {
            return TakeRefusal.LengthMismatch;
        }

        return IsInsideTrack(check.StartSample, check.LengthSamples, check.TrackSamples) ? TakeRefusal.None : TakeRefusal.OutsideTrack;
    }

    /// <summary>True when an adjustment lies within +/-200 ms.</summary>
    /// <param name="adjustmentSamples">The adjustment.</param>
    /// <returns>Whether it is allowed.</returns>
    public static bool IsValidAdjustment(int adjustmentSamples) => Math.Abs((long)adjustmentSamples) <= RecordingConstants.MaxAdjustmentSamples;
}

/// <summary>The values <see cref="TakeRules.Check"/> needs.</summary>
/// <param name="ExistingTakes">Takes the track already has (0 for a replacement).</param>
/// <param name="Speed">Playback speed while recording (1 for a replacement).</param>
/// <param name="TransposeSemitones">Transpose while recording.</param>
/// <param name="StartSample">First song sample.</param>
/// <param name="LengthSamples">Declared length.</param>
/// <param name="WavFrames">Frames of the uploaded WAV.</param>
/// <param name="TrackSamples">Track length in samples.</param>
public sealed record TakeCheck(int ExistingTakes, double Speed, int TransposeSemitones, long StartSample, long LengthSamples, long WavFrames, long TrackSamples);
