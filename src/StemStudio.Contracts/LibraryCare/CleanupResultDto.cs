namespace StemStudio.Contracts.LibraryCare;

/// <summary>Result of an executed clean-up (<c>CleanupResult</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record CleanupResultDto
{
    /// <summary>The executed plan.</summary>
    public required Guid PlanId { get; init; }

    /// <summary>Bytes freed.</summary>
    public required long FreedBytes { get; init; }

    /// <summary>What was removed.</summary>
    public required IReadOnlyList<CleanupRemovedDto> Removed { get; init; }

    /// <summary>What could not be removed.</summary>
    public required IReadOnlyList<CleanupSkipDto> Failed { get; init; }
}
