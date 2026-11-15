using StemStudio.Api.Storage;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Domain.Lyrics;
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
        if (track is null)
        {
            return NotFound(id);
        }

        await ScheduleOwnLookupAsync(track, cancellationToken);
        return TypedResults.Ok(TrackMapper.ToDto(track));
    }

    /// <summary>
    /// Schedules the lookup and the lyrics search of a completed track that never had them (005 FR-012, 006 FR-013:
    /// tracks imported before those features get them the first time they are opened) and saves.
    /// </summary>
    /// <param name="track">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task ScheduleOwnLookupAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (track.Status == TrackStatus.Completed && (track.Metadata.Status == MetadataStatus.None || track.LyricsStatus == LyricsStatus.None))
        {
            track.ScheduleOwnLookup(Now);
            track.ScheduleLyricsLookup(Now);
            await tracks.SaveAsync(cancellationToken);
        }
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
            return await ApplyRetryAsync(track, await CreateJobAsync(track, cancellationToken), cancellationToken);
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

    private async Task<IResult> ApplyRetryAsync(Track track, WorkerJob job, CancellationToken cancellationToken)
    {
        track.Retry(Now);
        track.AssignJob(job.JobId, Now);
        await tracks.SaveAsync(cancellationToken);
        return TypedResults.Accepted(ApiRoutes.Track(track.Id), TrackMapper.ToDto(track));
    }

    private Task<WorkerJob> CreateJobAsync(Track track, CancellationToken cancellationToken) => track.UploadFileName is { } fileName
        ? worker.CreateUploadJobAsync(new WorkerUpload(uploads.GetPath(fileName), fileName), cancellationToken)
        : worker.CreateUrlJobAsync(new WorkerUrlJobRequest(track.SourceType, track.Key.CanonicalUrl!), cancellationToken);
}
