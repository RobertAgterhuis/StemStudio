namespace StemStudio.Domain.Recording;

/// <summary>The stem a take replaces in compare mode (feature 015, data-model 2.1); <see cref="Piano"/> is labelled "Keys".</summary>
public enum RecordingPart
{
    /// <summary>No original part.</summary>
    None,

    /// <summary>Vocals.</summary>
    Vocals,

    /// <summary>Guitar.</summary>
    Guitar,

    /// <summary>Bass.</summary>
    Bass,

    /// <summary>Keys (stem <c>piano</c>).</summary>
    Piano,

    /// <summary>Drums.</summary>
    Drums,

    /// <summary>Acoustic and other instruments.</summary>
    Other,
}

/// <summary>Input channel mode of a take.</summary>
public enum InputChannelMode
{
    /// <summary>Channel 1 as mono.</summary>
    Mono1,

    /// <summary>Channel 2 as mono.</summary>
    Mono2,

    /// <summary>Channels 1 and 2 as stereo.</summary>
    Stereo,
}

/// <summary>How the latency of a take was obtained.</summary>
public enum LatencyMethod
{
    /// <summary>Loopback click test.</summary>
    Loopback,

    /// <summary>Entered by the user.</summary>
    Manual,

    /// <summary>Browser estimate.</summary>
    Estimated,
}

/// <summary>How a stored calibration was obtained.</summary>
public enum CalibrationMethod
{
    /// <summary>Loopback click test.</summary>
    Loopback,

    /// <summary>Entered by the user.</summary>
    Manual,
}

/// <summary>Binding constants of play-along recording (data-model "Constants"; mirrored by the contract's <c>RecordingLimits</c>).</summary>
public static class RecordingConstants
{
    /// <summary>Sample rate of every take.</summary>
    public const int SampleRate = 44_100;

    /// <summary>Bit depth of the uploaded WAV and the stored FLAC.</summary>
    public const int BitDepth = 24;

    /// <summary>Shortest take (0.5 s).</summary>
    public const long MinTakeSamples = 22_050;

    /// <summary>Longest take (15 minutes).</summary>
    public const long MaxTakeSamples = 15L * 60 * SampleRate;

    /// <summary>Samples a take may reach past the end of the track.</summary>
    public const long OutsideTrackTolerance = 4_410;

    /// <summary>Takes per track.</summary>
    public const int MaxTakesPerTrack = 200;

    /// <summary>Loop passes per session.</summary>
    public const int MaxPasses = 50;

    /// <summary>Highest latency (1 s).</summary>
    public const int MaxLatencySamples = SampleRate;

    /// <summary>Largest timing adjustment either way (200 ms).</summary>
    public const int MaxAdjustmentSamples = 8_820;

    /// <summary>Longest take name.</summary>
    public const int MaxNameLength = 80;

    /// <summary>Longest device label.</summary>
    public const int MaxDeviceLabelLength = 200;

    /// <summary>Longest device id hint.</summary>
    public const int MaxDeviceIdHintLength = 512;

    /// <summary>Highest loopback spread.</summary>
    public const int MaxSpreadSamples = 44;

    /// <summary>Fewest loopback clicks.</summary>
    public const int MinDetectedClicks = 6;

    /// <summary>Most loopback clicks.</summary>
    public const int MaxDetectedClicks = 12;

    /// <summary>Peak below which a take is quiet.</summary>
    public const double QuietPeakDbfs = -50;

    /// <summary>Lowest reported peak.</summary>
    public const double FloorDbfs = -150;
}
