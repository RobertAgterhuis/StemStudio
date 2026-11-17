using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Worker;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Storage;

/// <summary>The 30-second cache of the storage overview (invalidated by a clean-up).</summary>
/// <param name="clock">Clock.</param>
/// <param name="options">API options.</param>
public sealed class StorageOverviewCache(TimeProvider clock, IOptions<StemStudioApiOptions> options)
{
    private StorageOverviewDto? _cached;

    /// <summary>The cached overview when it is younger than the cache time.</summary>
    /// <returns>The overview, or null.</returns>
    public StorageOverviewDto? Fresh() =>
        _cached is { } cached && clock.GetUtcNow() - cached.MeasuredAt < TimeSpan.FromSeconds(options.Value.LibraryCare.StorageCacheSeconds) ? cached : null;

    /// <summary>Stores an overview.</summary>
    /// <param name="overview">The overview.</param>
    public void Store(StorageOverviewDto overview) => _cached = overview;

    /// <summary>Forgets the cached overview.</summary>
    public void Invalidate() => _cached = null;
}

/// <summary>
/// The storage overview (FR-020, FR-021, research R-10): the classified walk of the library (authoritative), the worker's
/// storage report (attributed to tracks through <c>Track.WorkerJobId</c>), free space per volume and the backups folder.
/// </summary>
/// <param name="context">The live database.</param>
/// <param name="walker">The library walker.</param>
/// <param name="worker">The worker client.</param>
/// <param name="volumes">Volume measurement.</param>
/// <param name="cache">The cache.</param>
/// <param name="clock">Clock.</param>
public sealed class StorageOverviewService(StemStudioDbContext context, LibraryWalker walker, IWorkerClient worker, StorageVolumes volumes, StorageOverviewCache cache, TimeProvider clock)
{
    /// <summary>The overview.</summary>
    /// <param name="refresh">Bypass the cache.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The overview.</returns>
    public async Task<StorageOverviewDto> GetAsync(bool refresh, CancellationToken cancellationToken)
    {
        if (!refresh && cache.Fresh() is { } cached)
        {
            return cached;
        }

        var overview = await MeasureAsync(cancellationToken);
        cache.Store(overview);
        return overview;
    }

    private async Task<StorageOverviewDto> MeasureAsync(CancellationToken cancellationToken)
    {
        var index = await LibraryIndexReader.ReadAsync(context, cancellationToken);
        var files = walker.Walk(index);
        var (storage, reason) = await WorkerStorageAsync(cancellationToken);
        var tracks = await context.Tracks.AsNoTracking().ToListAsync(cancellationToken);
        var attribution = new StorageAttribution(files, storage, await AssetTracksAsync(cancellationToken), await GenerationTracksAsync(cancellationToken));
        return new StorageOverviewDto
        {
            MeasuredAt = clock.GetUtcNow(),
            Volumes = volumes.Measure(storage, reason),
            Categories = StorageCategories.Usage(files, storage),
            Tracks = [.. tracks.Select(attribution.Row).OrderByDescending(row => row.TotalBytes)],
            Unattached = attribution.Unattached(await context.LyricsSheets.CountAsync(cancellationToken)),
            ModelCaches = [.. (storage?.ModelCaches ?? []).Select(model => new ModelCacheUsageDto { Id = model.Id, Bytes = model.Bytes, Models = model.Models })],
            WorkerEntries = WorkerEntries.Of(storage, tracks),
            Backups = volumes.Backups(),
            Worker = new WorkerStorageStateDto { Available = storage is not null, Reason = reason },
        };
    }

    private async Task<Dictionary<Guid, Guid?>> AssetTracksAsync(CancellationToken cancellationToken) =>
        (await context.Assets.AsNoTracking().ToListAsync(cancellationToken)).ToDictionary(asset => asset.Id, asset => asset.TrackId);

    private async Task<Dictionary<Guid, Guid?>> GenerationTracksAsync(CancellationToken cancellationToken) =>
        (await context.Generations.AsNoTracking().ToListAsync(cancellationToken)).ToDictionary(generation => generation.Id, generation => generation.SourceTrackId);

    private async Task<(WorkerStorage? Storage, string? Reason)> WorkerStorageAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await worker.GetStorageAsync(cancellationToken), null);
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            return (null, "The processing service is not reachable; its disk use cannot be shown.");
        }
    }
}

/// <summary>The worker cache entries of the overview (<c>workerEntries</c>, shown at Expert).</summary>
public static class WorkerEntries
{
    /// <summary>One row per worker cache entry, largest first, with the track whose standard job uses it.</summary>
    /// <param name="storage">The worker report, or null when the worker is not reachable.</param>
    /// <param name="tracks">The library tracks.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<WorkerCacheEntryStorageDto> Of(WorkerStorage? storage, IReadOnlyList<Track> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        var trackByJob = tracks.Where(track => track.WorkerJobId is not null).GroupBy(track => track.WorkerJobId!).ToDictionary(group => group.Key, group => group.First().Id);
        return [.. (storage?.CacheEntries ?? []).Select(entry => Row(entry, trackByJob)).OrderByDescending(row => row.Bytes)];
    }

    private static WorkerCacheEntryStorageDto Row(WorkerCacheEntry entry, Dictionary<string, Guid> trackByJob) => new()
    {
        AudioSha256Prefix = entry.AudioSha256[..12],
        Bytes = entry.StandardBytes + entry.DetailBytes + entry.PartialBytes + entry.ArtworkBytes,
        InUse = entry.InUse,
        FromLibrary = entry.FromLibrary,
        TrackId = entry.JobIds.Select(job => trackByJob.TryGetValue(job, out var id) ? id : (Guid?)null).FirstOrDefault(id => id is not null),
    };
}

/// <summary>Usage per storage category.</summary>
public static class StorageCategories
{
    private static readonly Dictionary<StorageCategory, (string How, string Cost)> Regeneration = new()
    {
        [StorageCategory.DetailStems] = ("Split further on the track page", "a few minutes per song on the GPU"),
        [StorageCategory.Leftovers] = ("Not needed; removed by the clean-up", "none"),
        [StorageCategory.RestoreStaging] = ("Removed when the restore ends", "none"),
        [StorageCategory.WorkerSeparationCache] = ("Created again automatically when needed", "a few seconds per song"),
        [StorageCategory.WorkerDetailCache] = ("Created again by Split further", "a few minutes per song on the GPU"),
        [StorageCategory.WorkerJobFiles] = ("Not needed after a job ended", "none"),
        [StorageCategory.WorkerScratch] = ("Not needed after a job ended", "none"),
    };

    /// <summary>Every category with bytes and files; worker categories are null without a worker report.</summary>
    /// <param name="files">The classified library files.</param>
    /// <param name="storage">The worker report, or null.</param>
    /// <returns>The usage.</returns>
    public static IReadOnlyList<StorageCategoryUsageDto> Usage(IReadOnlyList<WalkedFile> files, WorkerStorage? storage)
    {
        ArgumentNullException.ThrowIfNull(files);
        return [.. Enum.GetValues<StorageCategory>().Select(category => Usage(category, Measure(category, files, storage)))];
    }

    private static (long? Bytes, int? Files) Measure(StorageCategory category, IReadOnlyList<WalkedFile> files, WorkerStorage? storage) => category switch
    {
        StorageCategory.WorkerSeparationCache => (storage?.CacheEntries.Sum(entry => entry.StandardBytes), storage?.CacheEntries.Count),
        StorageCategory.WorkerDetailCache => (storage?.CacheEntries.Sum(entry => entry.DetailBytes + entry.PartialBytes), storage?.CacheEntries.Count(entry => entry.DetailBytes + entry.PartialBytes > 0)),
        StorageCategory.WorkerJobFiles => (storage?.JobFilesBytes, storage?.JobFilesCount),
        StorageCategory.WorkerScratch => (storage?.ScratchBytes, null),
        _ => (files.Where(file => file.Classification.Category == category).Sum(file => file.Bytes), files.Count(file => file.Classification.Category == category)),
    };

    private static StorageCategoryUsageDto Usage(StorageCategory category, (long? Bytes, int? Files) measured)
    {
        var regenerable = Regeneration.TryGetValue(category, out var how);
        return new StorageCategoryUsageDto
        {
            Category = (StorageCategoryDto)(int)category,
            Bytes = measured.Bytes,
            Files = measured.Files,
            Regenerability = (RegenerabilityDto)(int)Regenerabilities.Of(category),
            HowToRegenerate = regenerable ? how.How : null,
            CostHint = regenerable ? how.Cost : null,
        };
    }
}

/// <summary>Attributes library files and worker cache entries to tracks (data-model 6.1).</summary>
/// <param name="files">The classified library files.</param>
/// <param name="storage">The worker report, or null.</param>
/// <param name="assetTracks">Track of every library item.</param>
/// <param name="generationTracks">Source track of every generation.</param>
public sealed class StorageAttribution(
    IReadOnlyList<WalkedFile> files, WorkerStorage? storage, IReadOnlyDictionary<Guid, Guid?> assetTracks, IReadOnlyDictionary<Guid, Guid?> generationTracks)
{
    private readonly ILookup<Guid?, WalkedFile> _byEntity = files.ToLookup(file => file.Classification.EntityId);

    /// <summary>The row of a track.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The row.</returns>
    public TrackStorageDto Row(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var own = _byEntity[track.Id].ToList();
        var entry = EntryOf(track);
        var source = Source(track, entry);
        var row = new TrackStorageDto
        {
            TrackId = track.Id,
            Title = track.Title,
            Artist = track.Artist,
            TotalBytes = 0,
            SourceBytes = source,
            StemsBytes = Sum(own, StorageCategory.Stems),
            DetailStemsBytes = Sum(own, StorageCategory.DetailStems),
            DetailFilesRemoved = track.DetailPhase?.FilesRemovedAt is not null,
            ArtworkBytes = Sum(own, StorageCategory.Artwork),
            ExportsBytes = Linked(track.Id, StorageCategory.Exports),
            ScoresBytes = Linked(track.Id, StorageCategory.Scores),
            GenerationsBytes = Linked(track.Id, StorageCategory.Generations),
            RecordingsBytes = Linked(track.Id, StorageCategory.Recordings),
            WorkerCacheBytes = WorkerCache(entry),
        };
        return row with { TotalBytes = (row.SourceBytes ?? 0) + row.StemsBytes + row.DetailStemsBytes + row.ArtworkBytes + row.ExportsBytes + row.ScoresBytes + row.GenerationsBytes + row.RecordingsBytes };
    }

    /// <summary>Items without a track.</summary>
    /// <param name="lyricsSheets">Number of lyrics sheets.</param>
    /// <returns>The unattached usage.</returns>
    public UnattachedStorageDto Unattached(int lyricsSheets) => new()
    {
        ExportsBytes = Linked(null, StorageCategory.Exports),
        ScoresBytes = Linked(null, StorageCategory.Scores),
        GenerationsBytes = Linked(null, StorageCategory.Generations),
        VoiceProfilesBytes = files.Where(file => file.Classification.Category == StorageCategory.VoiceProfiles).Sum(file => file.Bytes),
        LyricsSheets = lyricsSheets,
    };

    private WorkerCacheEntry? EntryOf(Track track) =>
        track.WorkerJobId is { } job ? storage?.CacheEntries.FirstOrDefault(candidate => candidate.JobIds.Contains(job)) : null;

    private long? Source(Track track, WorkerCacheEntry? entry)
    {
        var pending = track.UploadFileName is { } upload ? files.Where(file => file.RelativePath == "uploads/" + upload).Sum(file => file.Bytes) : 0;
        return storage is null && pending == 0 ? null : pending + (entry?.StandardBytes ?? 0);
    }

    private long? WorkerCache(WorkerCacheEntry? entry) => storage is null ? null : (entry?.StandardBytes ?? 0) + (entry?.DetailBytes ?? 0) + (entry?.PartialBytes ?? 0);

    private static long Sum(IEnumerable<WalkedFile> own, StorageCategory category) => own.Where(file => file.Classification.Category == category).Sum(file => file.Bytes);

    private long Linked(Guid? trackId, StorageCategory category) =>
        files.Where(file => file.Classification.Category == category && file.Classification.EntityId is { } entity && TrackOf(entity) == trackId).Sum(file => file.Bytes);

    private Guid? TrackOf(Guid entity) =>
        assetTracks.TryGetValue(entity, out var track) ? track : generationTracks.GetValueOrDefault(entity);
}

/// <summary>Free space of the volumes and the backups folder.</summary>
/// <param name="paths">Library-care folders.</param>
/// <param name="files">Free-space seam.</param>
/// <param name="folder">The backups folder.</param>
/// <param name="catalog">Archives.</param>
public sealed class StorageVolumes(LibraryCarePaths paths, ILibraryFiles files, BackupFolder folder, BackupCatalog catalog)
{
    /// <summary>The four volumes; volumes with the same free and total bytes are on one disk.</summary>
    /// <param name="storage">The worker report, or null.</param>
    /// <param name="reason">Why the worker volumes are unknown.</param>
    /// <returns>The volumes.</returns>
    public IReadOnlyList<StorageVolumeDto> Measure(WorkerStorage? storage, string? reason)
    {
        var measured = new (StorageVolumeIdDto Id, string Label, VolumeSpace? Space)[]
        {
            (StorageVolumeIdDto.Library, "Library", files.Space(paths.Data.Root)),
            (StorageVolumeIdDto.Backups, "Backups folder", Directory.Exists(paths.BackupsDirectory) ? files.Space(paths.BackupsDirectory) : null),
            (StorageVolumeIdDto.WorkerData, "Processing data", Worker(storage, "data")),
            (StorageVolumeIdDto.ModelCache, "Model cache", Worker(storage, "models")),
        };
        return [.. measured.Select(volume => new StorageVolumeDto
        {
            Id = volume.Id, Label = volume.Label, FreeBytes = volume.Space?.FreeBytes, TotalBytes = volume.Space?.TotalBytes,
            SharedWith = [.. measured.Where(other => other.Id != volume.Id && volume.Space is not null && other.Space == volume.Space).Select(other => Name(other.Id))],
            Reason = volume.Space is null ? (volume.Id is StorageVolumeIdDto.WorkerData or StorageVolumeIdDto.ModelCache ? reason : "The folder cannot be measured.") : null,
        })];
    }

    /// <summary>Archives in the backups folder.</summary>
    /// <returns>Count, bytes and the folder state.</returns>
    public BackupsStorageDto Backups()
    {
        var state = folder.Probe();
        var archives = catalog.List();
        return new BackupsStorageDto { Count = archives.Count, Bytes = archives.Sum(archive => archive.SizeBytes), FolderAvailable = state.Writable, Reason = state.Reason };
    }

    private static VolumeSpace? Worker(WorkerStorage? storage, string id) =>
        storage?.Volumes.FirstOrDefault(volume => volume.Id == id) is { } volume ? new VolumeSpace(volume.FreeBytes, volume.TotalBytes) : null;

    private static string Name(StorageVolumeIdDto id) => id switch
    {
        StorageVolumeIdDto.Library => "library",
        StorageVolumeIdDto.Backups => "backups",
        StorageVolumeIdDto.WorkerData => "workerData",
        _ => "modelCache",
    };
}
