using Microsoft.Extensions.Options;
using StemStudio.Api.Tracks;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Insights;

/// <summary>
/// Reads and starts the stem quality report of a track (FR-001, FR-002, data-model 5.1). Check, store and start run under one
/// lock (<see cref="QualityGate"/>), so two requests never start two reports.
/// </summary>
/// <param name="tracks">Tracks.</param>
/// <param name="worker">The worker insights lane.</param>
/// <param name="inputs">Plans the worker request.</param>
/// <param name="view">Builds the report view.</param>
/// <param name="gate">The start lock.</param>
/// <param name="options">The <c>AutoQualityReport</c> setting.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class QualityService(
    ITrackRepository tracks,
    IWorkerInsightsClient worker,
    QualityInputs inputs,
    QualityView view,
    QualityGate gate,
    IOptions<InsightsOptions> options,
    TimeProvider clock,
    ILogger<QualityService> logger)
{
    /// <summary>Detail of <c>track-not-ready</c> for a track that is still being processed.</summary>
    public const string NotCompleted = "The track is still being processed; check the stems when it is completed.";

    /// <summary>Detail of <c>track-not-ready</c> for a track without the six standard stems.</summary>
    public const string NotSixStems = "The quality report needs the six-stem separation (vocals, drums, bass, guitar, piano and other).";

    /// <summary>The report error when the worker refuses a quality job (its reason is logged, never echoed).</summary>
    public const string WorkerRefused = "The processing service could not make the quality report of this track.";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary><c>GET /api/tracks/{trackId}/quality</c>.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid trackId, CancellationToken cancellationToken) =>
        await tracks.FindAsync(trackId, cancellationToken) is { } track
            ? TypedResults.Ok(await view.ReportAsync(track, cancellationToken))
            : TrackService.NotFound(trackId);

    /// <summary><c>POST /api/tracks/{trackId}/quality</c>: (re)makes the report.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 503.</returns>
    public Task<IResult> RequestAsync(Guid trackId, CancellationToken cancellationToken) => gate.RunAsync(
        async () => await tracks.FindAsync(trackId, cancellationToken) is { } track
            ? Refusal(track) ?? await StartLockedAsync(track, cancellationToken) ?? TypedResults.Accepted(ApiRoutes.Quality(trackId), await view.ReportAsync(track, cancellationToken))
            : TrackService.NotFound(trackId),
        cancellationToken);

    /// <summary>
    /// The automatic run after a standard or detail separation (setting <c>AutoQualityReport</c>): only for a completed six-stem
    /// track without an active report; an unreachable worker leaves the report as it was.
    /// </summary>
    /// <param name="track">The track (tracked by this scope).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when a report was queued (or stored as failed after a refusal).</returns>
    public async Task<bool> StartAutomaticallyAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (!options.Value.AutoQualityReport)
        {
            return false;
        }

        // The track was loaded before the lock: the stored report decides whether another request started one meanwhile.
        return await gate.RunAsync(
            async () => Refusal(track) is null && !await tracks.IsQualityActiveStoredAsync(track.Id, cancellationToken) && await StartLockedAsync(track, cancellationToken) is null,
            cancellationToken);
    }

    private static IResult? Refusal(Track track) => track switch
    {
        { Status: not TrackStatus.Completed } => InsightsProblems.TrackNotReady(NotCompleted),
        _ when !QualityInputs.HasSixStems(track) => InsightsProblems.TrackNotReady(NotSixStems),
        { IsQualityActive: true } => InsightsProblems.QualityRunning(),
        _ => null,
    };

    /// <summary>Submits the job and stores the queued report; null on success (also for a refusal, stored as failed), else 503.</summary>
    private async Task<IResult?> StartLockedAsync(Track track, CancellationToken cancellationToken)
    {
        var plan = inputs.Plan(track);
        WorkerQualityJob job;
        try
        {
            job = await worker.CreateQualityJobAsync(plan.Request, cancellationToken);
        }
        catch (WorkerUnavailableException)
        {
            return InsightsProblems.WorkerUnavailable();
        }
        catch (WorkerRequestException refused)
        {
            track.RequestQuality(plan.StemsVersion, Now);
            LogRefused(logger, track.Id, refused.Detail);
            track.FailQuality(WorkerRefused, Now);
            await tracks.SaveAsync(cancellationToken);
            return null;
        }

        return await SubmittedJob.KeepAsync(() => StoreQueuedAsync(track, plan.StemsVersion, job.QualityJobId), () => worker.DeleteQualityJobAsync(job.QualityJobId, CancellationToken.None), logger);
    }

    /// <summary>Stores the queued report of an accepted job; no cancellation from here on (the job exists).</summary>
    private async Task<IResult?> StoreQueuedAsync(Track track, string stemsVersion, string workerJobId)
    {
        // The id is checked before the track changes, so a refused id leaves no queued report behind.
        var jobId = Domain.JobIds.Require(workerJobId, nameof(workerJobId));
        track.RequestQuality(stemsVersion, Now);
        track.AssignQualityJob(jobId, Now);
        await tracks.SaveAsync(CancellationToken.None);
        return null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Quality report of track {TrackId}: the processing service refused the job: {Reason}")]
    private static partial void LogRefused(ILogger logger, Guid trackId, string reason);
}
