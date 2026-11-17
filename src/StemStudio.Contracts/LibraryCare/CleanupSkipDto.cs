namespace StemStudio.Contracts.LibraryCare;

/// <summary>A skipped or failed clean-up item (<c>CleanupSkip</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record CleanupSkipDto
{
    /// <summary>What would have been removed.</summary>
    public required CleanupKindDto Kind { get; init; }

    /// <summary>The track; null for worker-wide items.</summary>
    public required Guid? TrackId { get; init; }

    /// <summary>Track title.</summary>
    public required string? Title { get; init; }

    /// <summary>Why it was skipped.</summary>
    public required CleanupSkipReasonDto Reason { get; init; }
}
