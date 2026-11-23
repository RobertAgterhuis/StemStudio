namespace StemStudio.Contracts.Insights;

/// <summary>Rating, estimates and tips of one stem or detail stem (backend-api 1.19.0, feature 020).</summary>
public sealed record StemQualityViewDto
{
    /// <summary>ChannelId.</summary>
    public required string Channel { get; init; }

    /// <summary>Parent stem of a detail or rest stem; null for standard stems.</summary>
    public required string? Parent { get; init; }

    /// <summary>Rating.</summary>
    public required QualityRatingDto Rating { get; init; }

    /// <summary>Reason text of data-model 4.6 (at most 300 characters).</summary>
    public required string Reason { get; init; }

    /// <summary>Rating of the last ready report before this one; null when none.</summary>
    public required QualityRatingDto? PreviousRating { get; init; }

    /// <summary>Bleed estimate.</summary>
    public required BleedViewDto Bleed { get; init; }

    /// <summary>Artefact estimate.</summary>
    public required ArtefactViewDto Artefacts { get; init; }

    /// <summary>At most two tips (<see cref="ContractLimits.MaxTipsPerStem"/>).</summary>
    public required IReadOnlyList<TipDto> Tips { get; init; }
}
