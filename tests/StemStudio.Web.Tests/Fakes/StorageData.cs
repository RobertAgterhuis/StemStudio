using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Storage overview and clean-up DTOs of backend API 1.12.0 (feature 013, US3/US4).</summary>
public static class StorageData
{
    public const long GiB = 1024L * 1024 * 1024;

    public const long MiB = 1024L * 1024;

    public static readonly Guid TrackA = Guid.Parse("e1b2c3d4-0000-4000-8000-0000000000a1");

    public static readonly Guid TrackB = Guid.Parse("e1b2c3d4-0000-4000-8000-0000000000b2");

    public static readonly Guid PlanId = Guid.Parse("11111111-2222-4333-8444-555555555555");

    public static readonly DateTimeOffset Measured = new(2026, 11, 17, 10, 20, 0, TimeSpan.Zero);

    public static StorageVolumeDto Volume(StorageVolumeIdDto id, long? free = 100 * GiB, long? total = 400 * GiB, IReadOnlyList<string>? shared = null) => new()
    {
        Id = id,
        Label = id switch
        {
            StorageVolumeIdDto.Library => "Library",
            StorageVolumeIdDto.Backups => "Backups folder",
            StorageVolumeIdDto.WorkerData => "Worker data",
            _ => "Model cache",
        },
        FreeBytes = free,
        TotalBytes = total,
        SharedWith = shared ?? [],
        Reason = null,
    };

    /// <summary>A category; one with a way to regenerate it (<paramref name="how"/>) is regenerable, otherwise original.</summary>
    public static StorageCategoryUsageDto Category(StorageCategoryDto category, long? bytes, int? files, string? how = null) => new()
    {
        Category = category,
        Bytes = bytes,
        Files = files,
        Regenerability = how is null ? RegenerabilityDto.Original : RegenerabilityDto.Regenerable,
        HowToRegenerate = how,
        CostHint = how is null ? null : "minutes per track",
    };

    public static TrackStorageDto Track(Guid id, string title, long total, long detail = 0) => new()
    {
        TrackId = id,
        Title = title,
        Artist = "Band",
        TotalBytes = total,
        SourceBytes = null,
        StemsBytes = total - detail,
        DetailStemsBytes = detail,
        DetailFilesRemoved = false,
        ArtworkBytes = 0,
        ExportsBytes = 0,
        ScoresBytes = 0,
        GenerationsBytes = 0,
        RecordingsBytes = 0,
        WorkerCacheBytes = null,
    };

    public static StorageOverviewDto Overview(long? libraryFree = 100 * GiB, bool workerAvailable = true, IReadOnlyList<TrackStorageDto>? tracks = null) => new()
    {
        MeasuredAt = Measured,
        Volumes =
        [
            Volume(StorageVolumeIdDto.Library, libraryFree, shared: ["workerData", "modelCache"]),
            Volume(StorageVolumeIdDto.Backups, 300 * GiB, 900 * GiB),
            Volume(StorageVolumeIdDto.WorkerData, workerAvailable ? 100 * GiB : null, workerAvailable ? 400 * GiB : null, ["library", "modelCache"]) with
            {
                Reason = workerAvailable ? null : "worker unavailable",
            },
            Volume(StorageVolumeIdDto.ModelCache, 100 * GiB, 400 * GiB, ["library", "workerData"]),
        ],
        Categories =
        [
            Category(StorageCategoryDto.Stems, 800 * MiB, 24),
            Category(StorageCategoryDto.DetailStems, 600 * MiB, 12, "Split further on the track page"),
            Category(StorageCategoryDto.Exports, 50 * MiB, 3),
            Category(StorageCategoryDto.Leftovers, 5 * MiB, 7, "Not needed"),
            Category(StorageCategoryDto.WorkerSeparationCache, workerAvailable ? 2 * GiB : null, workerAvailable ? 40 : null, "Rebuilt from the library stems"),
        ],
        Tracks = tracks ?? [Track(TrackA, "Big song", 900 * MiB, 600 * MiB), Track(TrackB, "Small song", 100 * MiB) with { DetailFilesRemoved = true }],
        Unattached = new UnattachedStorageDto { ExportsBytes = 10 * MiB, ScoresBytes = 1 * MiB, GenerationsBytes = 30 * MiB, VoiceProfilesBytes = 2 * MiB, LyricsSheets = 4 },
        ModelCaches = [new ModelCacheUsageDto { Id = "torch", Bytes = 3 * GiB, Models = ["htdemucs_6s", "htdemucs"] }],
        WorkerEntries = workerAvailable
            ?
            [
                new WorkerCacheEntryStorageDto { AudioSha256Prefix = "3f9a1c0e7b2d", Bytes = 2 * GiB, InUse = true, FromLibrary = false, TrackId = TrackA },
                new WorkerCacheEntryStorageDto { AudioSha256Prefix = "a1b2c3d4e5f6", Bytes = 5 * MiB, InUse = false, FromLibrary = true, TrackId = null },
            ]
            : [],
        Backups = new BackupsStorageDto { Count = 2, Bytes = 4 * GiB, FolderAvailable = true, Reason = null },
        Worker = new WorkerStorageStateDto { Available = workerAvailable, Reason = workerAvailable ? null : "The worker is not running." },
    };
}
