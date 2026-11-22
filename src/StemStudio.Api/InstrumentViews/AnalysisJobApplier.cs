using StemStudio.Api.Sync;
using StemStudio.Api.Worker;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.InstrumentViews;

/// <summary>
/// Polls the worker for queued and running analyses (data-model 7.2): progress and device, the completed document (checked
/// against the contract limits), the worker's failure text, or "interrupted" when the worker no longer knows the job
/// (restart). The worker is polled outside the lock; the state is applied under <see cref="AnalysisGate"/> only when the
/// analysis still runs the same job (a delete or re-run in between wins). Finished worker jobs are deleted.
/// </summary>
/// <param name="analyses">Analyses.</param>
/// <param name="worker">The worker analysis lane.</param>
/// <param name="gate">The lock shared with <see cref="TrackAnalysisService"/>.</param>
/// <param name="devices">Converts the reported device.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class AnalysisJobApplier(
    ITrackAnalysisRepository analyses, IWorkerAnalysisClient worker, AnalysisGate gate, JobDeviceApplier devices, TimeProvider clock, ILogger<AnalysisJobApplier> logger)
{
    /// <summary>Reason when the worker no longer knows the job (restart, clean-up).</summary>
    public const string JobLost = "The analysis was interrupted; try again.";

    /// <summary>Reason when the result breaks the contract.</summary>
    public const string InvalidResult = "The processing service sent an unusable result; try again.";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>One pass over every active analysis.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        foreach (var analysis in await analyses.ActiveAsync(cancellationToken))
        {
            try
            {
                await SyncAsync(analysis, cancellationToken);
            }
            catch (WorkerUnavailableException)
            {
                LogWorkerUnavailable(logger, analysis.TrackId, analysis.Kind);
                return;
            }
        }
    }

    private async Task SyncAsync(TrackAnalysis analysis, CancellationToken cancellationToken)
    {
        var jobId = analysis.JobId!;
        var job = await worker.GetAnalysisJobAsync(jobId, cancellationToken);
        var finished = await gate.RunAsync(() => ApplyCurrentAsync(analysis, jobId, job, cancellationToken), cancellationToken);
        if (finished && job is not null)
        {
            await worker.DeleteAnalysisJobAsync(jobId, cancellationToken);
        }
    }

    private async Task<bool> ApplyCurrentAsync(TrackAnalysis analysis, string jobId, WorkerAnalysisJob? job, CancellationToken cancellationToken)
    {
        if (await analyses.CurrentJobIdAsync(analysis.TrackId, analysis.Kind, cancellationToken) != jobId)
        {
            return job is not null;
        }

        var finished = Apply(analysis, job);
        await analyses.SaveAsync(cancellationToken);
        return finished;
    }

    private bool Apply(TrackAnalysis analysis, WorkerAnalysisJob? job)
    {
        var device = devices.Convert(analysis.TrackId, job?.Device);
        if (job is null || job.Status is WorkerPracticeJobState.Completed or WorkerPracticeJobState.Failed or WorkerPracticeJobState.Cancelled)
        {
            Finish(analysis, job, device);
            return true;
        }

        analysis.ApplyProgress(job.Status == WorkerPracticeJobState.Running, job.Progress, device, Now);
        return false;
    }

    private void Finish(TrackAnalysis analysis, WorkerAnalysisJob? job, Domain.Tracks.JobDevice? device)
    {
        if (job is { Status: WorkerPracticeJobState.Completed, Result: { } result })
        {
            Complete(analysis, result, device);
        }
        else
        {
            analysis.Fail(job is null ? JobLost : job.Error, Now);
        }
    }

    private void Complete(TrackAnalysis analysis, WorkerAnalysisResult result, Domain.Tracks.JobDevice? device)
    {
        if (AnalysisResultMapper.ToDocument(analysis.Kind, result) is { } document)
        {
            analysis.Complete(document, device, Now);
            return;
        }

        LogInvalidResult(logger, analysis.TrackId, analysis.Kind);
        analysis.Fail(InvalidResult, Now);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Analysis {Kind} of track {TrackId}: processing service unreachable, retrying on the next tick")]
    private static partial void LogWorkerUnavailable(ILogger logger, Guid trackId, AnalysisKind kind);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Analysis {Kind} of track {TrackId}: the result breaks the contract and was refused")]
    private static partial void LogInvalidResult(ILogger logger, Guid trackId, AnalysisKind kind);
}
