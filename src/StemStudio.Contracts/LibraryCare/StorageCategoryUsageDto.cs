namespace StemStudio.Contracts.LibraryCare;

/// <summary>Size of one storage category (<c>StorageCategoryUsage</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record StorageCategoryUsageDto
{
    /// <summary>The category.</summary>
    public required StorageCategoryDto Category { get; init; }

    /// <summary>Bytes; null when unknown (worker unavailable).</summary>
    public required long? Bytes { get; init; }

    /// <summary>Files; null when unknown.</summary>
    public required int? Files { get; init; }

    /// <summary>Whether StemStudio can recreate it.</summary>
    public required RegenerabilityDto Regenerability { get; init; }

    /// <summary>How to get it back (&lt;= 200 characters).</summary>
    public required string? HowToRegenerate { get; init; }

    /// <summary>What regenerating costs (&lt;= 120 characters).</summary>
    public required string? CostHint { get; init; }
}
