namespace StemStudio.Contracts.LibraryCare;

/// <summary>One worker cache entry of the storage overview (<c>WorkerCacheEntryStorage</c>, backend-api 1.12.0, feature 013).</summary>
public sealed record WorkerCacheEntryStorageDto
{
    /// <summary>First 12 hex characters of the cache key (the audio SHA-256).</summary>
    public required string AudioSha256Prefix { get; init; }

    /// <summary>Standard and detail results, unfinished detail checkpoints and artwork of the entry.</summary>
    public required long Bytes { get; init; }

    /// <summary>A queued or running worker job uses the entry.</summary>
    public required bool InUse { get; init; }

    /// <summary>Created by a rehydration from the library stems.</summary>
    public required bool FromLibrary { get; init; }

    /// <summary>The library track whose standard job uses the entry, or null.</summary>
    public required Guid? TrackId { get; init; }
}
