using System.Net;
using StemStudio.Api.Storage;
using StemStudio.Api.Tracks;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Detail;

/// <summary>Starts ("Split further", retry) and cancels the detail phase of a track (FR-008, FR-009).</summary>
/// <param name="tracks">Track repository.</param>
/// <param name="worker">Worker client (cancel only; submission is done by the <see cref="DetailSynchronizer"/>).</param>
/// <param name="files">Detail stem store.</param>
/// <param name="clock">Clock.</param>
/// <param name="locks">Per-track locks shared with the <see cref="DetailSynchronizer"/>: a start or cancel never interleaves with a sync step.</param>
public sealed class DetailService(ITrackRepository tracks, IWorkerClient worker, IDetailStemFileStore files, TimeProvider clock, DetailPhaseLocks locks)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Queues a fresh detail phase; the background sync submits it, so an unreachable worker is no error.</summary>
    /// <param name="id">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404 or 409.</returns>
    public Task<IResult> StartAsync(Guid id, CancellationToken cancellationToken) =>
        locks.RunAsync(id, () => StartLockedAsync(id, cancellationToken), cancellationToken);

    /// <summary>Cancels a queued or running detail phase (in the worker first when a worker job exists).</summary>
    /// <param name="id">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404, 409 or 503.</returns>
    public Task<IResult> CancelAsync(Guid id, CancellationToken cancellationToken) =>
        locks.RunAsync(id, () => CancelLockedAsync(id, cancellationToken), cancellationToken);

    private async Task<IResult> StartLockedAsync(Guid id, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(id, cancellationToken);
        if (track is null)
        {
            return TrackService.NotFound(id);
        }

        return StartBlocker(track) ?? await QueueAsync(track, cancellationToken);
    }

    private async Task<IResult> CancelLockedAsync(Guid id, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(id, cancellationToken);
        if (track is null)
        {
            return TrackService.NotFound(id);
        }

        if (!track.IsDetailActive)
        {
            return ApiProblems.Conflict("Detail phase not active", "Only a queued or running detail phase can be cancelled.");
        }

        return await CancelInWorkerAsync(track.DetailPhase!.WorkerDetailJobId, cancellationToken) ?? await CancelLocallyAsync(track, cancellationToken);
    }

    private static IResult? StartBlocker(Track track) => track switch
    {
        { Status: not TrackStatus.Completed } => ApiProblems.Conflict("Track not completed", "Only completed tracks can be split further."),
        { CanStartDetail: false } => ApiProblems.Conflict(
            "Detail phase cannot be started",
            $"The detail phase is {track.DetailPhase!.Status.ToString().ToLowerInvariant()}; it can be started only when it is none, failed or cancelled."),
        _ => null,
    };

    private async Task<IResult> QueueAsync(Track track, CancellationToken cancellationToken)
    {
        RemoveOldFiles(track.Id);
        track.RequestDetail(Now);
        await tracks.SaveAsync(cancellationToken);
        return TypedResults.Accepted(ApiRoutes.Track(track.Id), TrackMapper.ToDto(track));
    }

    /// <summary>Removes detail files left by an earlier phase; a leftover file is overwritten by the next completion anyway.</summary>
    private void RemoveOldFiles(Guid trackId)
    {
        try
        {
            files.DeleteAll(trackId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: the next completed phase writes every detail stem again (temp file + rename).
        }
    }

    /// <summary>Cancels the worker job; null when the phase may be cancelled locally (no job, or the worker forgot it).</summary>
    private Task<IResult?> CancelInWorkerAsync(string? detailJobId, CancellationToken cancellationToken) =>
        detailJobId is null ? Task.FromResult<IResult?>(null) : CancelJobAsync(detailJobId, cancellationToken);

    private async Task<IResult?> CancelJobAsync(string detailJobId, CancellationToken cancellationToken)
    {
        try
        {
            await worker.CancelDetailJobAsync(detailJobId, cancellationToken);
            return null;
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            return CancelProblem(exception);
        }
    }

    /// <summary>
    /// Unreachable worker: 503. A worker that no longer knows the job (404) allows a local cancel (null); a job that
    /// already finished in the worker (409) is a conflict.
    /// </summary>
    private static IResult? CancelProblem(Exception exception) => exception switch
    {
        WorkerRequestException { StatusCode: HttpStatusCode.NotFound } => null,
        WorkerRequestException rejection => ApiProblems.Conflict("Detail phase already finished", rejection.Detail),
        _ => ApiProblems.Unavailable(),
    };

    private async Task<IResult> CancelLocallyAsync(Track track, CancellationToken cancellationToken)
    {
        track.CancelDetail(Now);
        await tracks.SaveAsync(cancellationToken);
        return TypedResults.Ok(TrackMapper.ToDto(track));
    }
}
