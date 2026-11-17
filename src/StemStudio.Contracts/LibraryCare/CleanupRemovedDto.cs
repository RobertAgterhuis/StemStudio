namespace StemStudio.Contracts.LibraryCare;

/// <summary>What one clean-up item removed (<c>CleanupResult.removed[]</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record CleanupRemovedDto
{
    /// <summary>What was removed.</summary>
    public required CleanupKindDto Kind { get; init; }

    /// <summary>The track; null for worker-wide items.</summary>
    public required Guid? TrackId { get; init; }

    /// <summary>Files removed.</summary>
    public required int Files { get; init; }

    /// <summary>Bytes freed.</summary>
    public required long Bytes { get; init; }
}
