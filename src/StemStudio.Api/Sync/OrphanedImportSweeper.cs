using Microsoft.Extensions.Options;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Sync;

/// <summary>
/// Fails queued tracks that never got a worker job (the client went away between the insert and the worker call), so
/// they do not stay queued forever; the user can retry them.
/// </summary>
/// <param name="tracks">Track repository (scoped).</param>
/// <param name="options">API options (<see cref="StemStudioApiOptions.OrphanedImportTimeout"/>).</param>
/// <param name="clock">Clock.</param>
public sealed class OrphanedImportSweeper(ITrackRepository tracks, IOptions<StemStudioApiOptions> options, TimeProvider clock)
{
    /// <summary>Failure reason of an interrupted import.</summary>
    public const string ImportInterrupted = "Import interrupted";

    /// <summary>Fails every orphaned track older than the configured timeout and saves.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var orphans = await tracks.ListOrphanedAsync(now - options.Value.OrphanedImportTimeout, cancellationToken);
        foreach (var track in orphans)
        {
            track.Fail(ImportInterrupted, now);
        }

        await tracks.SaveAsync(cancellationToken);
    }
}
