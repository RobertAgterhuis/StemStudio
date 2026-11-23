namespace StemStudio.Contracts.Insights;

/// <summary>Artefact estimate of a stem (backend-api 1.19.0, feature 020).</summary>
public sealed record ArtefactViewDto
{
    /// <summary>Level.</summary>
    public required QualityLevelDto Level { get; init; }

    /// <summary>Artefact index 0..1.</summary>
    public required double Index { get; init; }

    /// <summary>Artefact kinds found (unique).</summary>
    public required IReadOnlyList<ArtefactKindDto> Kinds { get; init; }

    /// <summary>Reliability label.</summary>
    public required ReliabilityDto Reliability { get; init; }
}
