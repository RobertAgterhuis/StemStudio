namespace StemStudio.Contracts.Recording;

/// <summary>
/// Problem <c>type</c> codes of play-along recording (backend-api 1.14.0, feature 015); the full type is
/// <c>https://stemstudio.local/problems/</c> + code. <c>insufficient-disk-space</c> (007), <c>track-not-ready</c> and the
/// 013 gate codes are reused.
/// </summary>
public static class RecordingProblemTypes
{
    /// <summary>400: the upload is not a readable RIFF/WAVE file.</summary>
    public const string InvalidWav = "invalid-wav";

    /// <summary>415: a readable WAV in another format (not 24-bit PCM, not 44,100 Hz, more than 2 channels).</summary>
    public const string UnsupportedWavFormat = "unsupported-wav-format";

    /// <summary>422: the WAV's frame count differs from <c>lengthSamples</c>.</summary>
    public const string LengthMismatch = "length-mismatch";

    /// <summary>422: every sample is digital silence.</summary>
    public const string SilentTake = "silent-take";

    /// <summary>422: the take does not lie inside the track.</summary>
    public const string TakeOutsideTrack = "take-outside-track";

    /// <summary>409: the track already has 200 takes.</summary>
    public const string TakeLimitReached = "take-limit-reached";

    /// <summary>422: recorded at a speed other than 100 % or with a transpose.</summary>
    public const string RecordingSpeed = "recording-speed";

    /// <summary>409: an export names a take that is not ready.</summary>
    public const string TakeNotReady = "take-not-ready";

    /// <summary>422: an export names a take of another track.</summary>
    public const string TakeNotInTrack = "take-not-in-track";

    /// <summary>422: an export lists the channel <c>take</c> without naming a take.</summary>
    public const string TakeRequired = "take-required";

    /// <summary>409: retry of a take that did not fail.</summary>
    public const string TakeNotFailed = "take-not-failed";

    /// <summary>409: the take is still being saved.</summary>
    public const string TakeSaving = "take-saving";

    /// <summary>409: retry without the stored capture; record again.</summary>
    public const string CaptureMissing = "capture-missing";

    /// <summary>409: rename, delete or retry of a take's asset through the library endpoints.</summary>
    public const string ManagedByTake = "managed-by-take";
}
