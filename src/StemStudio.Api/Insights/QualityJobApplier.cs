using StemStudio.Api.Worker;
using StemStudio.Domain;
using StemStudio.Domain.Insights;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Insights;

/// <summary>
/// One pass over every queued or running quality report (run by the job sync tick): polls the worker job, applies it, saves,
/// and removes the worker job once the report is ready or failed. The worker's result is checked before it is stored. An
/// unreachable worker keeps the state for the next tick.
/// </summary>
/// <param name="tracks">Tracks.</param>
/// <param name="worker">The worker insights lane.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class QualityJobApplier(ITrackRepository tracks, IWorkerInsightsClient worker, TimeProvider clock, ILogger<QualityJobApplier> logger)
{
    /// <summary>Error when the worker no longer knows the job (e.g. after a restart).</summary>
    public const string JobLost = "The quality check was interrupted; try again.";

    /// <summary>Error when the worker's result breaks the contract rules.</summary>
    public const string InvalidResult = "The processing service sent an unusable quality result; try again.";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Synchronises every active report.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        foreach (var track in await tracks.ListQualityActiveAsync(cancellationToken))
        {
            try
            {
                await SyncAsync(track, cancellationToken);
            }
            catch (WorkerUnavailableException)
            {
                LogWorkerUnavailable(logger, track.Id);
                return;
            }
        }
    }

    private async Task SyncAsync(Track track, CancellationToken cancellationToken)
    {
        if (track.QualityReport!.WorkerJobId is not { } jobId)
        {
            return;
        }

        var job = await worker.GetQualityJobAsync(jobId, cancellationToken);
        var finished = Apply(track, job);
        await tracks.SaveAsync(cancellationToken);
        if (finished && job is not null)
        {
            await DeleteAsync(track.Id, jobId, cancellationToken);
        }
    }

    private bool Apply(Track track, WorkerQualityJob? job)
    {
        switch (job)
        {
            case null:
                track.FailQuality(JobLost, Now);
                return true;
            case { Status: WorkerRenderJobState.Completed, Result: { } result }:
                Complete(track, result);
                return true;
            case { Status: WorkerRenderJobState.Completed or WorkerRenderJobState.Failed or WorkerRenderJobState.Cancelled }:
                track.FailQuality(job.Error, Now);
                return true;
            default:
                track.ApplyQualityProgress(job.Status == WorkerRenderJobState.Running, job.Progress, Now);
                return false;
        }
    }

    private void Complete(Track track, WorkerQualityResult result)
    {
        var stems = (result.Stems ?? []).Where(stem => stem is not null).Select(stem => stem.ToDomain()).ToList();
        try
        {
            QualityResultRules.Validate(result.AnalyzerVersion, stems.Count == (result.Stems?.Count ?? 0) ? stems : throw new DomainValidationException("A stem is missing."));
        }
        catch (DomainValidationException invalid)
        {
            LogInvalid(logger, track.Id, invalid.Message);
            track.FailQuality(InvalidResult, Now);
            return;
        }

        track.CompleteQuality(result.AnalyzerVersion, stems, Now);
    }

    private async Task DeleteAsync(Guid trackId, string jobId, CancellationToken cancellationToken)
    {
        try
        {
            await worker.DeleteQualityJobAsync(jobId, cancellationToken);
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            LogDeleteFailed(logger, trackId, exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Quality report of track {TrackId}: processing service unreachable, retrying on the next tick")]
    private static partial void LogWorkerUnavailable(ILogger logger, Guid trackId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Quality report of track {TrackId}: the worker result was refused: {Reason}")]
    private static partial void LogInvalid(ILogger logger, Guid trackId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Quality report of track {TrackId}: the worker job could not be removed: {Reason}")]
    private static partial void LogDeleteFailed(ILogger logger, Guid trackId, string reason);
}
