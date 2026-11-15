using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Sync;

/// <summary>Polls the worker for active tracks; an unreachable worker keeps the state, one broken track never stops the others.</summary>
/// <param name="tracks">Track repository (scoped).</param>
/// <param name="worker">Worker client.</param>
/// <param name="applier">Applies job states to tracks.</param>
/// <param name="logger">Logger.</param>
public sealed partial class TrackSynchronizer(
    ITrackRepository tracks, IWorkerClient worker, TrackJobApplier applier, ILogger<TrackSynchronizer> logger) : ITrackSynchronizer
{
    /// <inheritdoc />
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        await TrackPass.RunAsync(
            await tracks.ListActiveAsync(cancellationToken), track => track.Id, track => SyncAsync(track, cancellationToken), (trackId, exception) => LogTrackSyncFailed(logger, trackId, exception));
    }

    private async Task SyncAsync(Track track, CancellationToken cancellationToken)
    {
        var (reachable, job) = await TryGetJobAsync(track, cancellationToken);
        if (!reachable)
        {
            return;
        }

        var before = track.Status;
        await applier.ApplyAsync(track, job, cancellationToken);
        await tracks.SaveAsync(cancellationToken);
        LogTransition(track, before);
    }

    private async Task<(bool Reachable, WorkerJob? Job)> TryGetJobAsync(Track track, CancellationToken cancellationToken)
    {
        try
        {
            return (true, await worker.GetJobAsync(track.WorkerJobId!, cancellationToken));
        }
        catch (WorkerUnavailableException)
        {
            LogWorkerUnavailable(logger, track.Id);
            return (false, null);
        }
    }

    private void LogTransition(Track track, TrackStatus before)
    {
        if (track.Status != before)
        {
            LogStatusChanged(logger, track.Id, before, track.Status, track.Error);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Track {TrackId}: {From} -> {To} {Error}")]
    private static partial void LogStatusChanged(ILogger logger, Guid trackId, TrackStatus from, TrackStatus to, string? error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Track {TrackId}: processing service unreachable, retrying on the next tick")]
    private static partial void LogWorkerUnavailable(ILogger logger, Guid trackId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Track {TrackId}: synchronisation failed unexpectedly")]
    private static partial void LogTrackSyncFailed(ILogger logger, Guid trackId, Exception exception);
}
