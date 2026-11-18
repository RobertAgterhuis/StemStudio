using StemStudio.Api.Sync;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Detail;

/// <summary>
/// One pass over every track with a queued or running detail phase (plan.md key flow 4): submits pending phases
/// (<c>POST /v1/detail-jobs</c>), polls the others and applies the state. An unreachable worker keeps the state, one
/// broken track never stops the others. Each track is synchronised under its <see cref="DetailPhaseLocks"/> lock and
/// skipped when its stored phase changed after the pass loaded it (a cancel in between), so a stale phase is never saved.
/// </summary>
/// <param name="tracks">Track repository (scoped).</param>
/// <param name="worker">Worker client.</param>
/// <param name="applier">Applies detail job states.</param>
/// <param name="rehydrator">Rehydrates the worker cache on a miss (feature 013).</param>
/// <param name="locks">Per-track locks shared with <see cref="DetailService"/>.</param>
/// <param name="logger">Logger.</param>
public sealed partial class DetailSynchronizer(
    ITrackRepository tracks,
    IWorkerClient worker,
    DetailJobApplier applier,
    WorkerCacheRehydrator rehydrator,
    DetailPhaseLocks locks,
    ILogger<DetailSynchronizer> logger)
{
    /// <summary>Synchronises every active detail phase and saves after each track.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        await TrackPass.RunAsync(
            await tracks.ListDetailActiveAsync(cancellationToken), track => track.Id, track => SyncLockedAsync(track, cancellationToken), (trackId, exception) => LogSyncFailed(logger, trackId, exception));
    }

    private Task<bool> SyncLockedAsync(Track track, CancellationToken cancellationToken) =>
        locks.RunAsync(track.Id, () => SyncWhenCurrentAsync(track, cancellationToken), cancellationToken);

    private async Task<bool> SyncWhenCurrentAsync(Track track, CancellationToken cancellationToken)
    {
        if (!await tracks.IsDetailPhaseCurrentAsync(track, cancellationToken))
        {
            LogChangedMeanwhile(logger, track.Id);
            return false;
        }

        await SyncAsync(track, cancellationToken);
        return true;
    }

    private async Task SyncAsync(Track track, CancellationToken cancellationToken)
    {
        var before = track.DetailPhase!.Status;
        try
        {
            await ApplyAsync(track, track.DetailPhase.WorkerDetailJobId, cancellationToken);
        }
        catch (WorkerUnavailableException)
        {
            LogWorkerUnavailable(logger, track.Id);
            return;
        }

        await tracks.SaveAsync(cancellationToken);
        LogTransition(track, before);
    }

    private async Task ApplyAsync(Track track, string? detailJobId, CancellationToken cancellationToken)
    {
        if (detailJobId is null)
        {
            await SubmitAsync(track, cancellationToken);
            return;
        }

        await applier.ApplyAsync(track, await worker.GetDetailJobAsync(detailJobId, cancellationToken), cancellationToken);
    }

    private async Task SubmitAsync(Track track, CancellationToken cancellationToken)
    {
        try
        {
            var submission = await rehydrator.SubmitAsync(
                track, standard => worker.CreateDetailJobAsync(standard, cancellationToken), () => ShowPreparingStemsAsync(track, cancellationToken), cancellationToken);
            await applier.ApplySubmissionAsync(track, submission, cancellationToken);
        }
        catch (WorkerRequestException rejection)
        {
            applier.ApplyRejection(track, rejection.Detail);
        }
    }

    private async Task ShowPreparingStemsAsync(Track track, CancellationToken cancellationToken)
    {
        applier.ApplyPreparingStems(track);
        await tracks.SaveAsync(cancellationToken);
    }

    private void LogTransition(Track track, DetailPhaseStatus before)
    {
        var phase = track.DetailPhase!;
        if (phase.Status != before)
        {
            LogStatusChanged(logger, track.Id, before, phase.Status, phase.Error);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Track {TrackId}: detail {From} -> {To} {Error}")]
    private static partial void LogStatusChanged(ILogger logger, Guid trackId, DetailPhaseStatus from, DetailPhaseStatus to, string? error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Track {TrackId}: processing service unreachable for the detail phase, retrying on the next tick")]
    private static partial void LogWorkerUnavailable(ILogger logger, Guid trackId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Track {TrackId}: detail phase changed since the pass started, synchronised on the next tick")]
    private static partial void LogChangedMeanwhile(ILogger logger, Guid trackId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Track {TrackId}: detail synchronisation failed unexpectedly")]
    private static partial void LogSyncFailed(ILogger logger, Guid trackId, Exception exception);
}
