using StemStudio.Api.Sync;
using StemStudio.Api.Worker;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Practice;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.ScoreFollow;

/// <summary>
/// Polls the worker for queued and rendering score views (data-model 3.5): a completed job copies the SVG pages to
/// <c>assets/&lt;id&gt;/view/</c> and stores the bars shifted by the view's offset; a failed job stores the reason; the worker
/// job is deleted after applying.
/// </summary>
/// <param name="views">Score views.</param>
/// <param name="worker">The worker practice lane.</param>
/// <param name="files">Stored pages.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class ScoreViewJobApplier(IScoreViewRepository views, IWorkerPracticeClient worker, ScoreViewFiles files, TimeProvider clock, ILogger<ScoreViewJobApplier> logger)
{
    /// <summary>Text when the worker no longer knows the job.</summary>
    public const string JobLost = "Preparing the score view was interrupted; try again.";

    /// <summary>Text when the worker's result breaks the contract limits.</summary>
    public const string InvalidResult = "The score view could not be made.";

    /// <summary>Text when a page is above <see cref="WorkerClient.MaxScoreViewPageBytes"/> (nothing of the view is kept).</summary>
    public const string TooLarge = "The score view is too large to show; try a shorter range or a simpler score.";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>One pass over every active view.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        foreach (var view in await views.ActiveAsync(cancellationToken))
        {
            try
            {
                await SyncAsync(view, cancellationToken);
            }
            catch (WorkerUnavailableException)
            {
                LogWorkerUnavailable(logger, view.AssetId);
                return;
            }
        }
    }

    private async Task SyncAsync(ScoreView view, CancellationToken cancellationToken)
    {
        var jobId = view.WorkerJobId!;
        var job = await worker.GetScoreViewJobAsync(jobId, cancellationToken);
        var finished = await ApplyAsync(view, job, cancellationToken);
        await views.SaveAsync(cancellationToken);
        if (finished && job is not null)
        {
            await worker.DeleteScoreViewJobAsync(jobId, cancellationToken);
        }
    }

    private async Task<bool> ApplyAsync(ScoreView view, WorkerScoreViewJob? job, CancellationToken cancellationToken)
    {
        switch (job)
        {
            case null:
                view.Fail(JobLost, Now);
                return true;
            case { Status: WorkerPracticeJobState.Completed, Result: { } result }:
                await CompleteAsync(view, job.JobId, result, cancellationToken);
                return true;
            case { Status: WorkerPracticeJobState.Running }:
                view.MarkRendering(Now);
                return false;
            case { Status: WorkerPracticeJobState.Queued }:
                return false;
            default:
                view.Fail(job.Error, Now);
                return true;
        }
    }

    private async Task CompleteAsync(ScoreView view, string jobId, WorkerScoreViewResult result, CancellationToken cancellationToken)
    {
        var pages = result.Pages ?? [];
        if (pages.Count is < 1 or > ScoreView.MaxPages || result.Bars is not { Count: >= 1 and <= ScoreView.MaxBars } bars)
        {
            view.Fail(InvalidResult, Now);
            return;
        }

        if (await SavePagesAsync(view.AssetId, jobId, pages.Count, cancellationToken))
        {
            view.Complete(pages.Count, bars, Now);
        }
        else
        {
            view.Fail(TooLarge, Now);
        }
    }

    /// <summary>Replaces the stored pages; false (and no pages kept) when one is above the size cap.</summary>
    private async Task<bool> SavePagesAsync(Guid assetId, string jobId, int count, CancellationToken cancellationToken)
    {
        files.Clear(assetId);
        try
        {
            for (var page = 1; page <= count; page++)
            {
                var number = page;
                await files.SaveAsync(assetId, number, (stream, ct) => worker.DownloadScoreViewPageAsync(jobId, number, stream, ct), cancellationToken);
            }

            return true;
        }
        catch (WorkerResponseTooLargeException)
        {
            files.Clear(assetId);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Score view {AssetId}: processing service unreachable, retrying on the next tick")]
    private static partial void LogWorkerUnavailable(ILogger logger, Guid assetId);
}

/// <summary>Queues the score view of a new 008 score as soon as it is ready (014 FR-014).</summary>
/// <param name="service">The score view service.</param>
public sealed class ScoreViewReadyHook(ScoreViewService service) : IAssetReadyHook
{
    /// <inheritdoc />
    public Task OnReadyAsync(Asset asset, CancellationToken cancellationToken) => service.QueueNewAsync(asset, cancellationToken);
}
