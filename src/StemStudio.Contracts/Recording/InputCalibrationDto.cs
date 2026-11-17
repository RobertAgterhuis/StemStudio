namespace StemStudio.Contracts.Recording;

/// <summary>A stored input-latency calibration (backend-api 1.14.0, feature 015).</summary>
public sealed record InputCalibrationDto
{
    /// <summary>Calibration id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Device label (unique).</summary>
    public required string DeviceLabel { get; init; }

    /// <summary>Last browser device id seen.</summary>
    public string? DeviceIdHint { get; init; }

    /// <summary>Round trip in samples.</summary>
    public required int LatencySamples { get; init; }

    /// <summary>The same in milliseconds.</summary>
    public required double LatencyMilliseconds { get; init; }

    /// <summary>Loopback or manual.</summary>
    public required CalibrationMethodDto Method { get; init; }

    /// <summary>Loopback spread.</summary>
    public int? SpreadSamples { get; init; }

    /// <summary>Loopback clicks found.</summary>
    public int? DetectedClicks { get; init; }

    /// <summary>Output latency snapshot.</summary>
    public required int OutputLatencySamples { get; init; }

    /// <summary>When it was measured or entered.</summary>
    public required DateTimeOffset MeasuredAt { get; init; }
}
