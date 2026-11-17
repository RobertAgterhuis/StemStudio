namespace StemStudio.Contracts.LibraryCare;

/// <summary>One requested clean-up (<c>CleanupAction</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record CleanupActionDto
{
    /// <summary>What to remove.</summary>
    public required CleanupKindDto Kind { get; init; }

    /// <summary>Tracks to clean; null or absent = all tracks (ignored for leftovers; &lt;= 10,000).</summary>
    public IReadOnlyList<Guid>? TrackIds { get; init; }
}
