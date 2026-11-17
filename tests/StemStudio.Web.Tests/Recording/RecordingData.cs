using System.Text.Json;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Tests.Recording;

/// <summary>Takes, calibrations and captures shaped like the backend 1.14.0 and engine 2.5.0 examples.</summary>
public static class RecordingData
{
    public static readonly Guid TrackId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
    public static readonly Guid SessionId = Guid.Parse("0b9f6c8e-58a5-4d2b-9f0e-0d7f1e2a3b4c");
    public static readonly Guid TakeId = Guid.Parse("3f2b8a51-2c1e-4a8e-9d55-6a1f0c3b9e01");
    public static readonly Guid CalibrationId = Guid.Parse("a1f0c3b9-6a1f-4a8e-9d55-2c1e3f2b8a51");

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);

    public static RecordingTakeDto Take(Guid? id = null, TakeStatusDto status = TakeStatusDto.Ready, int number = 1) => new()
    {
        Id = id ?? TakeId,
        TrackId = TrackId,
        Number = number,
        Name = $"Take {number}",
        Part = RecordingPartDto.Guitar,
        Status = status,
        SessionId = SessionId,
        Partial = false,
        StartSample = 441_000,
        LengthSamples = 352_800,
        StartSeconds = 10,
        DurationSeconds = 8,
        InputMode = InputChannelModeDto.Mono1,
        InputChannels = 1,
        Latency = new TakeLatencyDto { Samples = 1102, Milliseconds = 25.0, Method = LatencyMethodDto.Loopback, CalibrationId = CalibrationId },
        DeviceLabel = "USB Mic",
        Favorite = false,
        PeakDbfs = -6.2,
        ClippedSamples = 0,
        Dropouts = 0,
        Quiet = false,
        AdjustmentSamples = 0,
        AlignmentRevision = 0,
        SizeBytes = 1_200_000,
        AssetId = Guid.Parse("9d55a1f0-c3b9-4a8e-6a1f-2c1e3f2b8a51"),
        AudioUrl = status == TakeStatusDto.Ready ? $"/api/takes/{id ?? TakeId:D}/audio" : null,
        RecordedAt = new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero),
        CreatedAt = new DateTimeOffset(2026, 11, 17, 10, 0, 9, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 11, 17, 10, 0, 9, TimeSpan.Zero),
    };

    public static RecordingTakeListDto List(params RecordingTakeDto[] takes) =>
        new() { Items = takes, TotalSizeBytes = takes.Sum(t => t.SizeBytes) };

    public static InputCalibrationDto Calibration(string label = "USB Mic", int samples = 1102, int outputLatency = 300) => new()
    {
        Id = CalibrationId,
        DeviceLabel = label,
        LatencySamples = samples,
        LatencyMilliseconds = Math.Round(samples * 1000.0 / 44_100, 1),
        Method = CalibrationMethodDto.Loopback,
        SpreadSamples = 3,
        DetectedClicks = 8,
        OutputLatencySamples = outputLatency,
        MeasuredAt = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero),
    };

    public static RecordingTakeUploadDto Upload() => new()
    {
        SessionId = SessionId,
        Partial = false,
        StartSample = 441_000,
        LengthSamples = 352_800,
        InputMode = InputChannelModeDto.Mono1,
        LatencySamples = 1102,
        LatencyMethod = LatencyMethodDto.Loopback,
        Part = RecordingPartDto.Guitar,
        Speed = 1.0,
        TransposeSemitones = 0,
        Dropouts = 0,
        RecordedAt = new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero),
    };

    public static CapturedTake Capture(string key = "c1", int? pass = null, bool partial = false) => new()
    {
        CaptureKey = key,
        SessionId = SessionId,
        PassIndex = pass,
        Partial = partial,
        StartSample = 441_000,
        LengthSamples = 352_800,
        Channels = 1,
        InputMode = InputChannelModeDto.Mono1,
        LatencySamples = 1102,
        LatencyMethod = LatencyMethodDto.Loopback,
        PeakDbfs = -6.2,
        ClippedSamples = 0,
        Dropouts = 0,
        RecordedAt = new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero),
    };

    public static InputInfo Input(string label = "USB Mic", int channels = 1, int estimated = 1500, int output = 300) =>
        new("d1", label, channels, InputChannelModeDto.Mono1, 44_100, estimated, output);

    public static string Body<T>(T value) => JsonSerializer.Serialize(value, Json);
}
