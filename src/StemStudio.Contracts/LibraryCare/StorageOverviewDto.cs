namespace StemStudio.Contracts.LibraryCare;

/// <summary>Classified storage use of StemStudio (<c>StorageOverview</c>, cached 30 s) (backend-api 1.12.0, feature 013).</summary>
public sealed record StorageOverviewDto
{
    /// <summary>When it was measured.</summary>
    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>Free and total space per area.</summary>
    public required IReadOnlyList<StorageVolumeDto> Volumes { get; init; }

    /// <summary>Size per category.</summary>
    public required IReadOnlyList<StorageCategoryUsageDto> Categories { get; init; }

    /// <summary>Per track, sorted by total bytes descending.</summary>
    public required IReadOnlyList<TrackStorageDto> Tracks { get; init; }

    /// <summary>Items without a track.</summary>
    public required UnattachedStorageDto Unattached { get; init; }

    /// <summary>Model cache folders.</summary>
    public required IReadOnlyList<ModelCacheUsageDto> ModelCaches { get; init; }

    /// <summary>Worker cache entries, largest first; empty when the worker is not reachable.</summary>
    public required IReadOnlyList<WorkerCacheEntryStorageDto> WorkerEntries { get; init; }

    /// <summary>The backups folder.</summary>
    public required BackupsStorageDto Backups { get; init; }

    /// <summary>Availability of the worker part.</summary>
    public required WorkerStorageStateDto Worker { get; init; }
}
