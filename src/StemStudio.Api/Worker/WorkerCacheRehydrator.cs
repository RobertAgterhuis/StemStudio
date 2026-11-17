using System.Net;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Worker;

/// <summary>
/// Lazy rehydration of the worker cache (feature 013, FR-019): when the worker answers a request for a track's standard job
/// with a cache miss (404 or 409), the job is re-created from the library stems (<c>POST /v1/jobs/rehydrate</c>), stored on
/// the track and the request is retried once; a second failure surfaces the original error.
/// </summary>
/// <param name="worker">The worker client.</param>
/// <param name="clock">Clock.</param>
public sealed class WorkerCacheRehydrator(IWorkerClient worker, TimeProvider clock)
{
    /// <summary>Runs a request for the track's standard job with one rehydration on a cache miss.</summary>
    /// <typeparam name="T">The result.</typeparam>
    /// <param name="track">A completed track (changed when rehydrated; the caller saves it).</param>
    /// <param name="submit">The request, given the standard job id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result.</returns>
    public async Task<T> SubmitAsync<T>(Track track, Func<string, Task<T>> submit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(submit);
        try
        {
            return await submit(track.WorkerJobId!);
        }
        catch (WorkerRequestException original) when (IsCacheMiss(original) && track.Status == TrackStatus.Completed)
        {
            return await RetryAsync(track, submit, original, cancellationToken);
        }
    }

    /// <summary>True for the worker answers that mean "the standard job or its stems are gone".</summary>
    /// <param name="rejection">The rejection.</param>
    /// <returns>True for 404 and 409.</returns>
    public static bool IsCacheMiss(WorkerRequestException rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        return rejection.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict;
    }

    /// <summary>The rehydration request of a track.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The request.</returns>
    public static WorkerRehydrateRequest Request(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return new WorkerRehydrateRequest(track.Id, track.SourceType.ToString().ToLowerInvariant(), track.Key.Value, track.Model!, [.. track.Stems.Select(stem => stem.Name)]);
    }

    private async Task<T> RetryAsync<T>(Track track, Func<string, Task<T>> submit, WorkerRequestException original, CancellationToken cancellationToken)
    {
        try
        {
            var job = await worker.RehydrateJobAsync(Request(track), cancellationToken);
            track.RehydrateWorkerJob(job.JobId, clock.GetUtcNow().UtcDateTime);
            return await submit(job.JobId);
        }
        catch (Exception exception) when (exception is WorkerRequestException or WorkerUnavailableException)
        {
            throw original;
        }
    }
}
