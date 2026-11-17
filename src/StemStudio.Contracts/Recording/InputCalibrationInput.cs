namespace StemStudio.Contracts.Recording;

/// <summary>Body of <c>upsertInputCalibration</c> (upsert by device label) (backend-api 1.14.0, feature 015).</summary>
public sealed record InputCalibrationInput
{
    /// <summary>Device label, 1..200 characters.</summary>
    public required string DeviceLabel { get; init; }

    /// <summary>Last browser device id seen (&lt;= 512).</summary>
    public string? DeviceIdHint { get; init; }

    /// <summary>Round trip, 0..44,100.</summary>
    public required int LatencySamples { get; init; }

    /// <summary>Loopback or manual.</summary>
    public required CalibrationMethodDto Method { get; init; }

    /// <summary>Loopback spread, 0..44.</summary>
    public int? SpreadSamples { get; init; }

    /// <summary>Loopback clicks found, 6..12.</summary>
    public int? DetectedClicks { get; init; }

    /// <summary>Output latency snapshot, 0..44,100.</summary>
    public required int OutputLatencySamples { get; init; }
}
