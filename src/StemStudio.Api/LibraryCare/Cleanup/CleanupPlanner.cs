using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.LibraryCare.Storage;
using StemStudio.Api.Storage;
using StemStudio.Api.Worker;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;
using StemStudio.Persistence;
using Track = StemStudio.Domain.Tracks.Track;

namespace StemStudio.Api.LibraryCare.Cleanup;

/// <summary>A library file in a plan.</summary>
/// <param name="Kind">Clean-up kind.</param>
/// <param name="TrackId">Owning track, if any.</param>
/// <param name="Path">Data-relative path.</param>
/// <param name="Bytes">Size at preview time.</param>
public sealed record PlannedFile(CleanupKindDto Kind, Guid? TrackId, string Path, long Bytes);

/// <summary>A worker cache entry in a plan.</summary>
/// <param name="AudioSha256">Cache key.</param>
/// <param name="TrackId">Track, if attributed.</param>
/// <param name="Bytes">Size at preview time.</param>
public sealed record PlannedEntry(string AudioSha256, Guid? TrackId, long Bytes);

/// <summary>A stored plan (<c>library-care/cleanup/&lt;planId&gt;.json</c>, data-model 6.3).</summary>
public sealed record StoredCleanupPlan
{
    /// <summary>The plan as shown.</summary>
    public required CleanupPlanDto Plan { get; init; }

    /// <summary>Library files to remove.</summary>
    public required IReadOnlyList<PlannedFile> Files { get; init; }

    /// <summary>Worker entries to remove.</summary>
    public required IReadOnlyList<PlannedEntry> Entries { get; init; }

    /// <summary>Also remove worker scratch and old job files.</summary>
    public required bool WorkerLeftovers { get; init; }

    /// <summary>SHA-256 over the planned files and entries.</summary>
    public required string Fingerprint { get; init; }
}

/// <summary>
/// Plans clean-ups of regenerable data only (FR-022-FR-024, SC-007): detail stems of tracks, the worker cache and
/// leftovers, with the reasons why items are skipped. Nothing is removed; plans are valid for 10 minutes.
/// </summary>
/// <param name="context">The live database.</param>
/// <param name="walker">The library walker.</param>
/// <param name="worker">The worker client.</param>
/// <param name="paths">Library-care folders.</param>
/// <param name="options">API options.</param>
/// <param name="clock">Clock.</param>
public sealed class CleanupPlanner(StemStudioDbContext context, LibraryWalker walker, IWorkerClient worker, LibraryCarePaths paths, IOptions<StemStudioApiOptions> options, TimeProvider clock)
{
    /// <summary>How detail stems come back.</summary>
    public const string SplitFurther = "Split further on the track page (instant while the worker cache has the result)";

    /// <summary>Previews a clean-up and stores the plan.</summary>
    /// <param name="request">The actions.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The plan.</returns>
    public async Task<CleanupPlanDto> PreviewAsync(CleanupRequestDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tracks = await context.Tracks.AsNoTracking().ToListAsync(cancellationToken);
        var files = walker.Walk(await LibraryIndexReader.ReadAsync(context, cancellationToken));
        var activeExports = (await context.Assets.AsNoTracking().ToListAsync(cancellationToken)).Where(asset => asset.IsActive).Select(asset => asset.TrackId).ToHashSet();
        var storage = request.Actions.Any(action => action.Kind != CleanupKindDto.DetailStems) ? await StorageAsync(cancellationToken) : null;
        var builder = new PlanBuilder(tracks, files, storage);
        foreach (var action in request.Actions)
        {
            builder.Add(action, activeExports);
        }

        var stored = builder.Build(Guid.NewGuid(), clock.GetUtcNow(), TimeSpan.FromMinutes(options.Value.LibraryCare.CleanupPlanMinutes), paths.Data);
        Directory.CreateDirectory(paths.CleanupDirectory);
        await JsonFiles.WriteAsync(PlanPath(paths, stored.Plan.PlanId), stored, cancellationToken);
        return stored.Plan;
    }

    /// <summary>The file of a plan.</summary>
    /// <param name="paths">Library-care folders.</param>
    /// <param name="planId">Plan id.</param>
    /// <returns>The path.</returns>
    public static string PlanPath(LibraryCarePaths paths, Guid planId)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.CleanupDirectory, LibraryCarePaths.Id(planId) + ".json");
    }

    /// <summary>The fingerprint over planned files (path, size, last write) and worker entries.</summary>
    /// <param name="data">The data directory.</param>
    /// <param name="files">Planned files.</param>
    /// <param name="entries">Planned entries.</param>
    /// <returns>Lower-case hex SHA-256.</returns>
    public static string Fingerprint(DataDirectory data, IEnumerable<PlannedFile> files, IEnumerable<PlannedEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(data);
        var lines = files.Select(file => Describe(data, file.Path)).Concat(entries.Select(entry => "worker|" + entry.AudioSha256)).Order(StringComparer.Ordinal);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    private static string Describe(DataDirectory data, string path)
    {
        var info = new FileInfo(Path.Combine(data.Root, path.Replace('/', Path.DirectorySeparatorChar)));
        return info.Exists ? string.Create(CultureInfo.InvariantCulture, $"{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}") : path + "|missing";
    }

    private async Task<WorkerStorage?> StorageAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await worker.GetStorageAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            return null;
        }
    }

    private sealed class PlanBuilder(IReadOnlyList<Track> tracks, IReadOnlyList<WalkedFile> files, WorkerStorage? storage)
    {
        private readonly List<CleanupItemDto> _items = [];
        private readonly List<CleanupSkipDto> _skipped = [];
        private readonly List<PlannedFile> _files = [];
        private readonly List<PlannedEntry> _entries = [];
        private bool _workerLeftovers;

        public void Add(CleanupActionDto action, IReadOnlySet<Guid?> activeExports)
        {
            switch (action.Kind)
            {
                case CleanupKindDto.DetailStems:
                    AddDetails(Selected(action).ToList(), activeExports);
                    break;
                case CleanupKindDto.WorkerCache:
                    AddWorkerCache(action);
                    break;
                default:
                    AddLeftovers();
                    break;
            }
        }

        public StoredCleanupPlan Build(Guid planId, DateTimeOffset now, TimeSpan validity, DataDirectory data) => new()
        {
            Plan = new CleanupPlanDto { PlanId = planId, CreatedAt = now, ExpiresAt = now + validity, Items = _items, Skipped = _skipped, TotalBytes = _items.Sum(item => item.Bytes) },
            Files = _files,
            Entries = _entries,
            WorkerLeftovers = _workerLeftovers,
            Fingerprint = Fingerprint(data, _files, _entries),
        };

        private IEnumerable<Track> Selected(CleanupActionDto action) =>
            action.TrackIds is { } ids ? tracks.Where(track => ids.Contains(track.Id)) : tracks.Where(track => track.HasDetailFiles || track.IsDetailActive);

        private void AddDetails(List<Track> selected, IReadOnlySet<Guid?> activeExports)
        {
            if (selected.Count == 0)
            {
                _skipped.Add(new CleanupSkipDto { Kind = CleanupKindDto.DetailStems, TrackId = null, Title = null, Reason = CleanupSkipReasonDto.NothingToRemove });
            }

            selected.ForEach(track => AddDetail(track, activeExports));
        }

        private void AddDetail(Track track, IReadOnlySet<Guid?> activeExports)
        {
            var reason = track.IsDetailActive ? CleanupSkipReasonDto.DetailActive
                : activeExports.Contains(track.Id) ? CleanupSkipReasonDto.ExportActive
                : track.HasDetailFiles ? (CleanupSkipReasonDto?)null : CleanupSkipReasonDto.NothingToRemove;
            if (reason is { } why)
            {
                _skipped.Add(Skip(CleanupKindDto.DetailStems, track, why));
                return;
            }

            var detail = files.Where(file => file.Classification.EntityId == track.Id && file.Classification.Category == StorageCategory.DetailStems).ToList();
            _files.AddRange(detail.Select(file => new PlannedFile(CleanupKindDto.DetailStems, track.Id, file.RelativePath, file.Bytes)));
            _items.Add(new CleanupItemDto { Kind = CleanupKindDto.DetailStems, TrackId = track.Id, Title = track.Title, Files = detail.Count, Bytes = detail.Sum(file => file.Bytes), HowToRegenerate = SplitFurther });
        }

        private void AddWorkerCache(CleanupActionDto action)
        {
            if (storage is null)
            {
                _skipped.Add(new CleanupSkipDto { Kind = CleanupKindDto.WorkerCache, TrackId = null, Title = null, Reason = CleanupSkipReasonDto.WorkerUnavailable });
                return;
            }

            foreach (var entry in storage.CacheEntries)
            {
                var track = tracks.FirstOrDefault(candidate => candidate.WorkerJobId is { } job && entry.JobIds.Contains(job));
                if (action.TrackIds is null || (track is not null && action.TrackIds.Contains(track.Id)))
                {
                    AddEntry(entry, track);
                }
            }
        }

        private void AddEntry(WorkerCacheEntry entry, Track? track)
        {
            if (entry.InUse)
            {
                _skipped.Add(Skip(CleanupKindDto.WorkerCache, track, CleanupSkipReasonDto.JobActive));
                return;
            }

            var bytes = entry.StandardBytes + entry.DetailBytes + entry.PartialBytes + entry.ArtworkBytes;
            _entries.Add(new PlannedEntry(entry.AudioSha256, track?.Id, bytes));
            _items.Add(new CleanupItemDto { Kind = CleanupKindDto.WorkerCache, TrackId = track?.Id, Title = track?.Title, Files = 1, Bytes = bytes, HowToRegenerate = "Created again automatically when needed" });
        }

        private void AddLeftovers()
        {
            var leftovers = files.Where(file => file.Classification.Category == StorageCategory.Leftovers).ToList();
            _files.AddRange(leftovers.Select(file => new PlannedFile(CleanupKindDto.Leftovers, null, file.RelativePath, file.Bytes)));
            _workerLeftovers = storage is not null;
            var bytes = leftovers.Sum(file => file.Bytes) + (storage?.ScratchBytes ?? 0);
            _items.Add(new CleanupItemDto { Kind = CleanupKindDto.Leftovers, TrackId = null, Title = null, Files = leftovers.Count, Bytes = bytes, HowToRegenerate = "Not needed by StemStudio" });
        }

        private static CleanupSkipDto Skip(CleanupKindDto kind, Track? track, CleanupSkipReasonDto reason) =>
            new() { Kind = kind, TrackId = track?.Id, Title = track?.Title, Reason = reason };
    }
}
