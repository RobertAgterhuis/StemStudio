using StemStudio.Api.Storage;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tracks;

/// <summary>Track queries and retry (FR-008, FR-018).</summary>
/// <param name="tracks">Track repository.</param>
/// <param name="worker">Worker client.</param>
/// <param name="uploads">Upload store (retry source for uploads).</param>
/// <param name="clock">Clock.</param>
public sealed class TrackService(ITrackRepository tracks, IWorkerClient worker, IUploadStore uploads, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>All tracks, newest first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 with the tracks.</returns>
    public async Task<IResult> ListAsync(CancellationToken cancellationToken) =>
        TypedResults.Ok((await tracks.ListAsync(cancellationToken)).Select(TrackMapper.ToDto).ToList());

    /// <summary>One track.</summary>
    /// <param name="id">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(id, cancellationToken);
        return track is null ? NotFound(id) : TypedResults.Ok(TrackMapper.ToDto(track));
    }

    /// <summary>Starts a new worker job for a failed track; the track stays failed when the worker cannot be reached.</summary>
    /// <param name="id">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 503.</returns>
    public async Task<IResult> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(id, cancellationToken);
        if (track is null)
        {
            return NotFound(id);
        }

        return RetryBlocker(track) ?? await RestartAsync(track, cancellationToken);
    }

    /// <summary>The 404 problem for an unknown track.</summary>
    /// <param name="id">Track ID.</param>
    /// <returns>The problem.</returns>
    public static IResult NotFound(Guid id) => ApiProblems.NotFound($"Track {id} does not exist.");

    private IResult? RetryBlocker(Track track) => track switch
    {
        { CanRetry: false } => ApiProblems.Conflict("Track is not failed", "Only failed tracks can be retried."),
        { UploadFileName: { } fileName } when !uploads.Exists(fileName) =>
            ApiProblems.Conflict("Upload no longer available", "The uploaded file is no longer available; upload it again."),
        _ => null,
    };

    private async Task<IResult> RestartAsync(Track track, CancellationToken cancellationToken)
    {
        try
        {
            var job = await CreateJobAsync(track, cancellationToken);
            track.Retry(Now);
            track.AssignJob(job.JobId, Now);
            await tracks.SaveAsync(cancellationToken);
            return TypedResults.Accepted(ApiRoutes.Track(track.Id), TrackMapper.ToDto(track));
        }
        catch (WorkerUnavailableException)
        {
            return ApiProblems.Unavailable();
        }
        catch (WorkerRequestException rejection)
        {
            return ApiProblems.Conflict("Retry rejected", rejection.Detail);
        }
    }

    private Task<WorkerJob> CreateJobAsync(Track track, CancellationToken cancellationToken) => track.UploadFileName is { } fileName
        ? worker.CreateUploadJobAsync(new WorkerUpload(uploads.GetPath(fileName), fileName), cancellationToken)
        : worker.CreateUrlJobAsync(new WorkerUrlJobRequest(track.SourceType, track.Key.CanonicalUrl!), cancellationToken);
}
