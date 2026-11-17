namespace StemStudio.Api.Worker;

/// <summary>Free space of a worker volume (worker 1.7.0 <c>VolumeUsage</c>).</summary>
/// <param name="Id"><c>data</c> or <c>models</c>.</param>
/// <param name="FreeBytes">Free bytes.</param>
/// <param name="TotalBytes">Total bytes.</param>
public sealed record WorkerVolumeUsage(string Id, long FreeBytes, long TotalBytes);

/// <summary>One per-audio cache entry of the worker (<c>CacheEntryUsage</c>).</summary>
/// <param name="AudioSha256">Cache key.</param>
/// <param name="StandardBytes">result.json and stems.</param>
/// <param name="DetailBytes">Finished detail result.</param>
/// <param name="PartialBytes">Checkpoints of an unfinished detail phase.</param>
/// <param name="ArtworkBytes">Artwork.</param>
/// <param name="JobIds">Standard jobs whose audio is this entry.</param>
/// <param name="InUse">A queued or running job uses it.</param>
/// <param name="FromLibrary">Created by a rehydration.</param>
/// <param name="LastUsedAt">Last use, if known.</param>
public sealed record WorkerCacheEntry(
    string AudioSha256, long StandardBytes, long DetailBytes, long PartialBytes, long ArtworkBytes, IReadOnlyList<string> JobIds, bool InUse, bool FromLibrary, DateTimeOffset? LastUsedAt);

/// <summary>A model cache folder (<c>ModelCacheUsage</c>).</summary>
/// <param name="Id">Folder name.</param>
/// <param name="Bytes">Size.</param>
/// <param name="Models">Known model ids found in it.</param>
public sealed record WorkerModelCache(string Id, long Bytes, IReadOnlyList<string> Models);

/// <summary>The worker storage report (<c>GET /v1/storage</c>, worker 1.7.0).</summary>
/// <param name="MeasuredAt">Measurement time.</param>
/// <param name="Volumes">Data and model volumes.</param>
/// <param name="CacheEntries">Per-audio cache entries.</param>
/// <param name="JobFilesBytes">Job files of every lane.</param>
/// <param name="JobFilesCount">Number of job files.</param>
/// <param name="ScratchBytes">work/, uploads/ and artwork/.</param>
/// <param name="ModelCaches">Model caches per folder.</param>
public sealed record WorkerStorage(
    DateTimeOffset MeasuredAt, IReadOnlyList<WorkerVolumeUsage> Volumes, IReadOnlyList<WorkerCacheEntry> CacheEntries, long JobFilesBytes, int JobFilesCount, long ScratchBytes, IReadOnlyList<WorkerModelCache> ModelCaches);

/// <summary>A clean-up request (<c>POST /v1/storage/cleanup</c>).</summary>
/// <param name="CacheEntries">audioSha256 values, or exactly one <c>*</c> for every entry not in use.</param>
/// <param name="Scratch">Remove scratch.</param>
/// <param name="OldJobFiles">Remove terminal job files older than 7 days.</param>
public sealed record WorkerStorageCleanupRequest(IReadOnlyList<string> CacheEntries, bool Scratch, bool OldJobFiles);

/// <summary>A cache entry the worker did not remove.</summary>
/// <param name="AudioSha256">Cache key.</param>
/// <param name="Reason"><c>inUse</c> or <c>notFound</c>.</param>
public sealed record WorkerSkippedEntry(string AudioSha256, string Reason);

/// <summary>What a clean-up removed (<c>StorageCleanupResult</c>).</summary>
/// <param name="FreedBytes">Bytes freed.</param>
/// <param name="RemovedEntries">Removed cache keys.</param>
/// <param name="Skipped">Skipped entries.</param>
public sealed record WorkerStorageCleanupResult(long FreedBytes, IReadOnlyList<string> RemovedEntries, IReadOnlyList<WorkerSkippedEntry> Skipped);

/// <summary>A rehydration request (<c>POST /v1/jobs/rehydrate</c>).</summary>
/// <param name="TrackId">Track id (sent in lower-case D format).</param>
/// <param name="SourceType"><c>youtube</c>, <c>spotify</c> or <c>upload</c>.</param>
/// <param name="SourceKey">Source key.</param>
/// <param name="Model">Separation model.</param>
/// <param name="Stems">Stem names of the model.</param>
public sealed record WorkerRehydrateRequest(Guid TrackId, string SourceType, string SourceKey, string Model, IReadOnlyList<string> Stems);

/// <summary>A rehydrated, completed standard job (<c>RehydratedJob</c>).</summary>
/// <param name="JobId">Standard job id.</param>
/// <param name="AudioSha256">Cache key.</param>
/// <param name="Status">Always <c>completed</c>.</param>
/// <param name="Model">Model.</param>
/// <param name="Stems">Stems.</param>
/// <param name="CreatedAt">Creation time.</param>
public sealed record WorkerRehydratedJob(string JobId, string AudioSha256, string Status, string Model, IReadOnlyList<string> Stems, DateTimeOffset CreatedAt);
