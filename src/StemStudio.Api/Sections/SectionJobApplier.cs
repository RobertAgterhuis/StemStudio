using StemStudio.Api.Worker;
using StemStudio.Domain.Sections;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.SongSections;

/// <summary>
/// Polls the worker for queued and running section detections (data-model 3.3): a completed job replaces the sections
/// (new ids, origin detected, revision + 1), a failed job stores the reason; the worker job is deleted after applying.
/// </summary>
/// <param name="sections">Section sets.</param>
/// <param name="worker">The worker practice lane.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class SectionJobApplier(ISectionRepository sections, IWorkerPracticeClient worker, TimeProvider clock, ILogger<SectionJobApplier> logger)
{
    /// <summary>Text when the worker no longer knows the job (restart, 24 h clean-up).</summary>
    public const string JobLost = "The detection was interrupted; try again.";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>One pass over every active detection.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        foreach (var set in await sections.ActiveAsync(cancellationToken))
        {
            try
            {
                await SyncAsync(set, cancellationToken);
            }
            catch (WorkerUnavailableException)
            {
                LogWorkerUnavailable(logger, set.TrackId);
                return;
            }
        }
    }

    private async Task SyncAsync(TrackSections set, CancellationToken cancellationToken)
    {
        var jobId = set.WorkerJobId!;
        var job = await worker.GetSectionJobAsync(jobId, cancellationToken);
        var finished = Apply(set, job);
        await sections.SaveAsync(cancellationToken);
        if (finished && job is not null)
        {
            await worker.DeleteSectionJobAsync(jobId, cancellationToken);
        }
    }

    private bool Apply(TrackSections set, WorkerSectionJob? job)
    {
        switch (job)
        {
            case null:
                set.Fail(JobLost, Now);
                return true;
            case { Status: WorkerPracticeJobState.Completed, Result: { } result }:
                set.ApplyDetected(SectionMapper.ToDetection(result), Guid.NewGuid, Now);
                return true;
            case { Status: WorkerPracticeJobState.Completed or WorkerPracticeJobState.Failed or WorkerPracticeJobState.Cancelled }:
                set.Fail(job.Error, Now);
                return true;
            case { Status: WorkerPracticeJobState.Running }:
                set.MarkDetecting(Now);
                return false;
            default:
                return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sections of track {TrackId}: processing service unreachable, retrying on the next tick")]
    private static partial void LogWorkerUnavailable(ILogger logger, Guid trackId);
}
