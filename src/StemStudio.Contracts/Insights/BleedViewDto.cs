namespace StemStudio.Contracts.Insights;

/// <summary>Bleed estimate of a stem (backend-api 1.19.0, feature 020).</summary>
public sealed record BleedViewDto
{
    /// <summary>Level.</summary>
    public required QualityLevelDto Level { get; init; }

    /// <summary>Leakage share 0..1 after the co-occurrence guard.</summary>
    public required double Index { get; init; }

    /// <summary>Standard stem most of the bleed comes from; null when none.</summary>
    public required string? MainSource { get; init; }

    /// <summary>True when the co-occurrence guard capped the level.</summary>
    public required bool CoOccurrence { get; init; }

    /// <summary>Reliability label.</summary>
    public required ReliabilityDto Reliability { get; init; }
}
