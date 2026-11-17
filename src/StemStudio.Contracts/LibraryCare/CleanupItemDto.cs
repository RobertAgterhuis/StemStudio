namespace StemStudio.Contracts.LibraryCare;

/// <summary>One planned clean-up item (<c>CleanupItem</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record CleanupItemDto
{
    /// <summary>What is removed.</summary>
    public required CleanupKindDto Kind { get; init; }

    /// <summary>The track; null for worker-wide items.</summary>
    public required Guid? TrackId { get; init; }

    /// <summary>Track title.</summary>
    public required string? Title { get; init; }

    /// <summary>Files removed.</summary>
    public required int Files { get; init; }

    /// <summary>Bytes freed.</summary>
    public required long Bytes { get; init; }

    /// <summary>How to get it back (&lt;= 200 characters).</summary>
    public required string HowToRegenerate { get; init; }
}
