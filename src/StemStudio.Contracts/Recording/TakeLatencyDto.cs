namespace StemStudio.Contracts.Recording;

/// <summary>Latency compensation applied to a take (backend-api 1.14.0, feature 015).</summary>
public sealed record TakeLatencyDto
{
    /// <summary>Compensation in samples, 0..44,100.</summary>
    public required int Samples { get; init; }

    /// <summary>The same in milliseconds.</summary>
    public required double Milliseconds { get; init; }

    /// <summary>Where the latency came from.</summary>
    public required LatencyMethodDto Method { get; init; }

    /// <summary>The calibration used, when <see cref="Method"/> is loopback or manual.</summary>
    public Guid? CalibrationId { get; init; }
}
