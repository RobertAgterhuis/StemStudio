using StemStudio.Api.Recording;
using StemStudio.Api.Scores;
using StemStudio.Api.Sync;
using StemStudio.Api.Worker;
using StemStudio.Domain.Assets;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Assets;

/// <summary>
/// One pass over every queued or rendering item (data-model 4.5, run by the <see cref="JobSyncService"/> tick): polls the
/// render job, applies it, saves, and deletes the worker job once the item is finished. An unreachable worker keeps the
/// state; one broken item never stops the others.
/// </summary>
/// <param name="assets">Asset repository (scoped).</param>
/// <param name="worker">Worker client.</param>
/// <param name="applier">Applies render job states.</param>
/// <param name="scores">Applies transcription job states (feature 008).</param>
/// <param name="finisher">Finishes ready takes (feature 015).</param>
/// <param name="logger">Logger.</param>
public sealed partial class AssetSynchronizer(
    IAssetRepository assets, IWorkerClient worker, AssetJobApplier applier, ScoreJobApplier scores, TakeAssetFinisher finisher, ILogger<AssetSynchronizer> logger)
{
    /// <summary>Synchronises every active item.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        var active = (await assets.ListActiveAsync(cancellationToken)).Where(asset => asset.WorkerJobId is not null);
        await TrackPass.RunAsync(active, asset => asset.Id, asset => SyncAsync(asset, cancellationToken), (id, exception) => LogSyncFailed(logger, id, exception));
    }

    private async Task SyncAsync(Asset asset, CancellationToken cancellationToken)
    {
        var jobId = asset.WorkerJobId!;
        try
        {
            await PollAsync(asset, jobId, cancellationToken);
        }
        catch (WorkerUnavailableException)
        {
            LogWorkerUnavailable(logger, asset.Id);
            return;
        }

        await assets.SaveAsync(cancellationToken);
        await finisher.FinishAsync(asset, cancellationToken);
        await DeleteFinishedJobAsync(asset, jobId, cancellationToken);
    }

    /// <summary>Polls and applies the render job, or the transcription job of a score item.</summary>
    private async Task PollAsync(Asset asset, string jobId, CancellationToken cancellationToken) => await (asset.Kind == AssetKind.Score
        ? scores.ApplyAsync(asset, await worker.GetTranscriptionJobAsync(jobId, cancellationToken), cancellationToken)
        : applier.ApplyAsync(asset, await worker.GetRenderJobAsync(jobId, cancellationToken), cancellationToken));

    /// <summary>Removes the worker's copy once the item is ready or failed (best effort; the worker sweeps after 24 h).</summary>
    private async Task DeleteFinishedJobAsync(Asset asset, string jobId, CancellationToken cancellationToken)
    {
        if (asset.IsActive)
        {
            return;
        }

        try
        {
            await AssetJobs.DeleteAsync(worker, asset, jobId, cancellationToken);
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            LogDeleteFailed(logger, asset.Id, exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Item {AssetId}: processing service unreachable, retrying on the next tick")]
    private static partial void LogWorkerUnavailable(ILogger logger, Guid assetId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Item {AssetId}: the render job could not be removed from the processing service: {Reason}")]
    private static partial void LogDeleteFailed(ILogger logger, Guid assetId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Item {AssetId}: synchronisation failed unexpectedly")]
    private static partial void LogSyncFailed(ILogger logger, Guid assetId, Exception exception);
}
