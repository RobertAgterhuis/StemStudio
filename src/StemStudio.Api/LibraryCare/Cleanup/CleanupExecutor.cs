using Microsoft.EntityFrameworkCore;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.LibraryCare.Storage;
using StemStudio.Api.Worker;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Cleanup;

/// <summary>
/// Executes a previewed plan (FR-025, FR-026): refused when unknown or expired (<c>plan-expired</c>) or when the planned files
/// changed (<c>plan-stale</c>); every library file is classified again right before it is removed, so only regenerable
/// categories are ever deleted; detail stems mark their track; worker entries go to the worker, which skips entries in use.
/// </summary>
/// <param name="context">The live database.</param>
/// <param name="worker">The worker client.</param>
/// <param name="paths">Library-care folders.</param>
/// <param name="cache">The storage overview cache (invalidated).</param>
/// <param name="clock">Clock.</param>
public sealed class CleanupExecutor(StemStudioDbContext context, IWorkerClient worker, LibraryCarePaths paths, StorageOverviewCache cache, TimeProvider clock)
{
    /// <summary>Executes a plan.</summary>
    /// <param name="planId">Plan id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result, or the problem code when refused.</returns>
    public async Task<(CleanupResultDto? Result, string? Problem)> ExecuteAsync(Guid planId, CancellationToken cancellationToken)
    {
        var path = CleanupPlanner.PlanPath(paths, planId);
        var stored = await JsonFiles.ReadAsync<StoredCleanupPlan>(path, cancellationToken);
        if (stored is null || clock.GetUtcNow() > stored.Plan.ExpiresAt)
        {
            File.Delete(path);
            return (null, LibraryCareProblemTypes.PlanExpired);
        }

        if (CleanupPlanner.Fingerprint(paths.Data, stored.Files, stored.Entries) != stored.Fingerprint)
        {
            return (null, LibraryCareProblemTypes.PlanStale);
        }

        File.Delete(path);
        var result = await RunAsync(stored, cancellationToken);
        cache.Invalidate();
        return (result, null);
    }

    private async Task<CleanupResultDto> RunAsync(StoredCleanupPlan plan, CancellationToken cancellationToken)
    {
        var index = await LibraryIndexReader.ReadAsync(context, cancellationToken);
        var removed = new List<CleanupRemovedDto>();
        foreach (var group in plan.Files.GroupBy(file => (file.Kind, file.TrackId)))
        {
            var deleted = group.Where(file => Delete(file, index)).ToList();
            removed.Add(new CleanupRemovedDto { Kind = group.Key.Kind, TrackId = group.Key.TrackId, Files = deleted.Count, Bytes = deleted.Sum(file => file.Bytes) });
        }

        await MarkDetailRemovedAsync(plan, cancellationToken);
        var failed = new List<CleanupSkipDto>();
        var workerFreed = await CleanWorkerAsync(plan, removed, failed, cancellationToken);
        return new CleanupResultDto { PlanId = plan.Plan.PlanId, FreedBytes = removed.Sum(item => item.Bytes) + workerFreed, Removed = removed, Failed = failed };
    }

    private bool Delete(PlannedFile file, LibraryIndex index)
    {
        var kind = file.Kind == CleanupKindDto.DetailStems ? CleanupKind.DetailStems : CleanupKind.Leftovers;
        var category = StorageClassifier.Classify(file.Path, index)?.Category;
        var full = Path.Combine(paths.Data.Root, file.Path.Replace('/', Path.DirectorySeparatorChar));
        if (category is not { } current || !CleanupRules.Removes(kind, current) || !File.Exists(full))
        {
            return false;
        }

        File.Delete(full);
        return true;
    }

    private async Task MarkDetailRemovedAsync(StoredCleanupPlan plan, CancellationToken cancellationToken)
    {
        var ids = plan.Files.Where(file => file.Kind == CleanupKindDto.DetailStems).Select(file => file.TrackId).OfType<Guid>().ToHashSet();
        foreach (var track in await context.Tracks.Where(track => ids.Contains(track.Id)).ToListAsync(cancellationToken))
        {
            if (track.HasDetailFiles)
            {
                track.RemoveDetailFiles(clock.GetUtcNow().UtcDateTime);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<long> CleanWorkerAsync(StoredCleanupPlan plan, List<CleanupRemovedDto> removed, List<CleanupSkipDto> failed, CancellationToken cancellationToken)
    {
        if (plan.Entries.Count == 0 && !plan.WorkerLeftovers)
        {
            return 0;
        }

        try
        {
            var result = await worker.CleanStorageAsync(new WorkerStorageCleanupRequest([.. plan.Entries.Select(entry => entry.AudioSha256)], plan.WorkerLeftovers, plan.WorkerLeftovers), cancellationToken);
            removed.AddRange(plan.Entries.Where(entry => result.RemovedEntries.Contains(entry.AudioSha256))
                .Select(entry => new CleanupRemovedDto { Kind = CleanupKindDto.WorkerCache, TrackId = entry.TrackId, Files = 1, Bytes = 0 }));
            failed.AddRange(plan.Entries.Where(entry => result.Skipped.Any(skip => skip.AudioSha256 == entry.AudioSha256))
                .Select(entry => new CleanupSkipDto { Kind = CleanupKindDto.WorkerCache, TrackId = entry.TrackId, Title = null, Reason = CleanupSkipReasonDto.JobActive }));
            return result.FreedBytes;
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            failed.Add(new CleanupSkipDto { Kind = CleanupKindDto.WorkerCache, TrackId = null, Title = null, Reason = CleanupSkipReasonDto.WorkerUnavailable });
            return 0;
        }
    }
}
