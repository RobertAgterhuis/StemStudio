namespace StemStudio.Contracts.Recording;

/// <summary>Binding constants of play-along recording (backend-api 1.14.0; data-model.md "Constants").</summary>
public static class RecordingLimits
{
    /// <summary>Sample rate of the engine context and of every take.</summary>
    public const int SampleRate = 44_100;

    /// <summary>Bit depth of the uploaded WAV and of the stored FLAC.</summary>
    public const int BitDepth = 24;

    /// <summary>Shortest take in samples (0.5 s).</summary>
    public const long MinTakeSamples = 22_050;

    /// <summary>Longest track a take may span, in samples (15 minutes).</summary>
    public const long MaxTakeSamples = 15L * 60 * SampleRate;

    /// <summary>Default request body limit of the two take upload endpoints (<c>StemStudio:Recording:MaxUploadBytes</c>, 256 MiB).</summary>
    public const long DefaultMaxUploadBytes = 256L * 1024 * 1024;

    /// <summary>Takes per track.</summary>
    public const int MaxTakesPerTrack = 200;

    /// <summary>Loop passes per recording session.</summary>
    public const int MaxPassesPerSession = 50;

    /// <summary>Seconds of capture per session (30 minutes).</summary>
    public const int MaxSessionSeconds = 1_800;

    /// <summary>Shortest kept partial pass in samples (1 s).</summary>
    public const long MinPartialPassSamples = SampleRate;

    /// <summary>Highest latency in samples (1,000 ms); the lowest is 0.</summary>
    public const int MaxLatencySamples = SampleRate;

    /// <summary>Largest timing adjustment in samples either way (200 ms).</summary>
    public const int MaxAdjustmentSamples = 8_820;

    /// <summary>Output-latency difference (samples, 5 ms) above which a stored calibration is reported as outdated.</summary>
    public const int SetupChangeSamples = 220;

    /// <summary>Longest take name in characters (trimmed, no control characters).</summary>
    public const int MaxTakeNameLength = 80;

    /// <summary>Longest device label in characters.</summary>
    public const int MaxDeviceLabelLength = 200;

    /// <summary>Longest device id hint in characters.</summary>
    public const int MaxDeviceIdHintLength = 512;

    /// <summary>Highest loopback spread in samples (1 ms).</summary>
    public const int MaxSpreadSamples = 44;

    /// <summary>Fewest clicks of a valid loopback calibration.</summary>
    public const int MinDetectedClicks = 6;

    /// <summary>Most clicks of a loopback calibration.</summary>
    public const int MaxDetectedClicks = 12;

    /// <summary>Peak (dBFS) below which a take is labelled quiet.</summary>
    public const double QuietPeakDbfs = -50;

    /// <summary>Longest readable error of a failed take.</summary>
    public const int MaxErrorLength = 500;

    /// <summary>Milliseconds to samples (data-model rounding rule): <c>sign(ms) * floor(abs(ms) * 44.1 + 0.5)</c>.</summary>
    /// <param name="milliseconds">Milliseconds.</param>
    /// <returns>Samples at 44.1 kHz.</returns>
    public static long ToSamples(double milliseconds) =>
        Math.Sign(milliseconds) * (long)Math.Floor((Math.Abs(milliseconds) * SampleRate / 1000.0) + 0.5);
}
