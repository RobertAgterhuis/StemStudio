using System.Text.Json.Serialization;
using StemStudio.Contracts.Recording;

namespace StemStudio.Web.Recording;

/// <summary>Recording state of the engine (<c>RecordingState</c> in types.ts 2.5.0).</summary>
public enum RecordingState
{
    /// <summary>No input open.</summary>
    Closed,

    /// <summary>Input open, not recording.</summary>
    Open,

    /// <summary>Count-in before the first captured sample.</summary>
    CountIn,

    /// <summary>Capturing.</summary>
    Recording,

    /// <summary>Stopped; waiting for the latency tail and the last batch.</summary>
    Finishing,
}

/// <summary>Microphone permission (<c>PermissionStateValue</c> in types.ts 2.5.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MicrophoneAccess>))]
public enum MicrophoneAccess
{
    /// <summary>Not queryable.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,

    /// <summary>Granted.</summary>
    [JsonStringEnumMemberName("granted")]
    Granted,

    /// <summary>The browser will ask.</summary>
    [JsonStringEnumMemberName("prompt")]
    Prompt,

    /// <summary>Denied.</summary>
    [JsonStringEnumMemberName("denied")]
    Denied,
}

/// <summary>Take/original compare mode (<c>TakeCompareMode</c> in types.ts 2.5.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TakeCompareMode>))]
public enum TakeCompareMode
{
    /// <summary>Take and original part audible.</summary>
    [JsonStringEnumMemberName("both")]
    Both,

    /// <summary>Only the take (the original part gated).</summary>
    [JsonStringEnumMemberName("take")]
    Take,

    /// <summary>Only the original (the take gated).</summary>
    [JsonStringEnumMemberName("original")]
    Original,
}

/// <summary>Result of <c>recordingSupport()</c>.</summary>
/// <param name="Supported">True when recording can be offered.</param>
/// <param name="ReasonCode"><c>insecureContext</c>, <c>noMediaDevices</c>, <c>noAudioWorklet</c>, <c>rateMismatch</c> or null.</param>
/// <param name="Reason">Readable reason, or null.</param>
/// <param name="Permission">The microphone permission.</param>
public sealed record RecordingSupport(bool Supported, string? ReasonCode, string? Reason, MicrophoneAccess Permission);

/// <summary>One audio input device.</summary>
/// <param name="DeviceId">Opaque browser id.</param>
/// <param name="Label">Label; empty before permission.</param>
/// <param name="GroupId">Group of one physical interface.</param>
public sealed record InputDevice(string DeviceId, string Label, string GroupId);

/// <summary>Argument of <c>openInput</c>.</summary>
/// <param name="DeviceId">Device, or null for the default input.</param>
/// <param name="Mode">Channel mode.</param>
public sealed record OpenInputRequest(string? DeviceId, InputChannelModeDto Mode);

/// <summary>The open input (result of <c>openInput</c>).</summary>
/// <param name="DeviceId">Device id.</param>
/// <param name="Label">Device label.</param>
/// <param name="ChannelCount">1 or 2.</param>
/// <param name="Mode">Applied mode.</param>
/// <param name="DeviceSampleRate">Device rate, or null.</param>
/// <param name="EstimatedLatencySamples">Latency estimate until calibrated.</param>
/// <param name="OutputLatencySamples">Output latency snapshot compared with a stored calibration.</param>
public sealed record InputInfo(
    string DeviceId,
    string Label,
    int ChannelCount,
    InputChannelModeDto Mode,
    double? DeviceSampleRate,
    int EstimatedLatencySamples,
    int OutputLatencySamples);

/// <summary>Direct monitoring settings.</summary>
/// <param name="Enabled">Input audible on the output.</param>
/// <param name="Volume">Gain 0..1.</param>
public sealed record MonitoringSettings(bool Enabled, double Volume);

/// <summary>Options of <c>calibrateLatency</c>.</summary>
/// <param name="Clicks">6..12 clicks, or null for the default 8.</param>
/// <param name="Seed">PRNG seed (tests), or null.</param>
public sealed record CalibrationOptions(int? Clicks = null, int? Seed = null);

/// <summary>Result of <c>calibrateLatency</c> (data-model 3.4).</summary>
/// <param name="Ok">True when measured.</param>
/// <param name="LatencySamples">Measured L, or null.</param>
/// <param name="LatencyMilliseconds">L in ms (0.1), or null.</param>
/// <param name="DetectedClicks">Clicks found.</param>
/// <param name="SpreadSamples">Spread in samples, or null.</param>
/// <param name="OutputLatencySamples">Output latency snapshot.</param>
/// <param name="InputPeakDbfs">Highest input peak.</param>
/// <param name="Reason"><c>noSignal</c>, <c>tooFewClicks</c>, <c>inconsistent</c>, <c>tooLong</c> or null.</param>
/// <param name="Warnings">Warnings such as <c>clipped</c>.</param>
public sealed record CalibrationResult(
    bool Ok,
    int? LatencySamples,
    double? LatencyMilliseconds,
    int DetectedClicks,
    int? SpreadSamples,
    int OutputLatencySamples,
    double InputPeakDbfs,
    string? Reason,
    IReadOnlyList<string> Warnings);

/// <summary>Argument of <c>startRecording</c>.</summary>
public sealed record RecordOptions
{
    /// <summary>L in samples, 0..44100.</summary>
    public required int LatencySamples { get; init; }

    /// <summary>Where L comes from.</summary>
    public required LatencyMethodDto LatencyMethod { get; init; }

    /// <summary>Recorded part.</summary>
    public required RecordingPartDto Part { get; init; }

    /// <summary>Gate the part's stem while recording.</summary>
    public required bool MuteOriginal { get; init; }

    /// <summary>Count-in bars 0..2.</summary>
    public required int CountInBars { get; init; }

    /// <summary>Loop passes 1..50, or null for the default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxPasses { get; init; }

    /// <summary>Seconds of capture per session, or null for the default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxSeconds { get; init; }
}

/// <summary>Progress of a running recording (<c>OnRecordingProgress</c>).</summary>
/// <param name="ElapsedSeconds">Seconds captured in this session.</param>
/// <param name="PassIndex">1-based loop pass, or null.</param>
/// <param name="CountInBeatsLeft">Count-in beats left, or null.</param>
public sealed record RecordingProgress(double ElapsedSeconds, int? PassIndex, int? CountInBeatsLeft);

/// <summary>One captured take (data-model 3.3).</summary>
public sealed record CapturedTake
{
    /// <summary>Engine-local id.</summary>
    public required string CaptureKey { get; init; }

    /// <summary>Session shared by the passes of one Record press.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>1-based loop pass, or null.</summary>
    public int? PassIndex { get; init; }

    /// <summary>Final pass shorter than the loop.</summary>
    public required bool Partial { get; init; }

    /// <summary>Song sample of the first sample.</summary>
    public required long StartSample { get; init; }

    /// <summary>Length in samples.</summary>
    public required long LengthSamples { get; init; }

    /// <summary>1 or 2.</summary>
    public required int Channels { get; init; }

    /// <summary>Channel mode.</summary>
    public required InputChannelModeDto InputMode { get; init; }

    /// <summary>L + P as applied.</summary>
    public required int LatencySamples { get; init; }

    /// <summary>Where L came from.</summary>
    public required LatencyMethodDto LatencyMethod { get; init; }

    /// <summary>Peak in dBFS.</summary>
    public required double PeakDbfs { get; init; }

    /// <summary>Samples at full scale.</summary>
    public required int ClippedSamples { get; init; }

    /// <summary>Frames filled because of gaps.</summary>
    public required int Dropouts { get; init; }

    /// <summary>Start of the recording (UTC).</summary>
    public required DateTimeOffset RecordedAt { get; init; }
}

/// <summary>Audio of a take for <c>loadTake</c>: a local capture or the saved FLAC.</summary>
public sealed record TakeSource
{
    /// <summary>Local capture key, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CaptureKey { get; init; }

    /// <summary>Absolute audio URL, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    /// <summary>Start sample of the saved take.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? StartSample { get; init; }

    /// <summary>Length of the saved take.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? LengthSamples { get; init; }

    /// <summary>Channels of the saved take.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Channels { get; init; }

    /// <summary>A local capture.</summary>
    /// <param name="captureKey">The engine key.</param>
    /// <returns>The source.</returns>
    public static TakeSource FromCapture(string captureKey) => new() { CaptureKey = captureKey };

    /// <summary>A saved take.</summary>
    /// <param name="url">Absolute audio URL.</param>
    /// <param name="startSample">Start sample.</param>
    /// <param name="lengthSamples">Length.</param>
    /// <param name="channels">1 or 2.</param>
    /// <returns>The source.</returns>
    public static TakeSource FromUrl(string url, long startSample, long lengthSamples, int channels) =>
        new() { Url = url, StartSample = startSample, LengthSamples = lengthSamples, Channels = channels };
}

/// <summary>Argument of <c>loadTake</c>.</summary>
/// <param name="TakeId">Backend id, or the capture key before saving.</param>
/// <param name="Source">The audio.</param>
/// <param name="Part">Part (compare gate).</param>
/// <param name="AdjustmentSamples">Playback-only shift, -8820..8820.</param>
public sealed record LoadTakeRequest(string TakeId, TakeSource Source, RecordingPartDto Part, int AdjustmentSamples = 0);
