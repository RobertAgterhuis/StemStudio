namespace StemStudio.Contracts.LibraryCare;

/// <summary>Storage used by one track (<c>TrackStorage</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record TrackStorageDto
{
    /// <summary>Track id.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Track title.</summary>
    public required string Title { get; init; }

    /// <summary>Artist.</summary>
    public required string? Artist { get; init; }

    /// <summary>Sum of the track's bytes.</summary>
    public required long TotalBytes { get; init; }

    /// <summary>Pending upload plus the worker separation cache of the track (null when the worker is unavailable and nothing is pending).</summary>
    public required long? SourceBytes { get; init; }

    /// <summary>Standard stems.</summary>
    public required long StemsBytes { get; init; }

    /// <summary>Detail stems.</summary>
    public required long DetailStemsBytes { get; init; }

    /// <summary>The detail stems were removed (<c>DetailPhase.filesRemovedAt</c> set).</summary>
    public required bool DetailFilesRemoved { get; init; }

    /// <summary>Song pictures.</summary>
    public required long ArtworkBytes { get; init; }

    /// <summary>Exports of the track.</summary>
    public required long ExportsBytes { get; init; }

    /// <summary>Sheet music of the track.</summary>
    public required long ScoresBytes { get; init; }

    /// <summary>Generations of the track.</summary>
    public required long GenerationsBytes { get; init; }

    /// <summary>Play-along takes of the track (feature 015; original, never in a clean-up).</summary>
    public required long RecordingsBytes { get; init; }

    /// <summary>Worker separation and detail cache of the track; null when the worker is unavailable.</summary>
    public required long? WorkerCacheBytes { get; init; }
}
