using System.Net;
using Microsoft.AspNetCore.Http.HttpResults;
using StemStudio.Api.Storage;
using StemStudio.Api.Tracks;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Importing;

/// <summary>Imports URLs and uploads: validates, de-duplicates by source key and starts the worker job (FR-001..FR-004, FR-009).</summary>
/// <param name="tracks">Track repository.</param>
/// <param name="worker">Worker client.</param>
/// <param name="uploads">Upload store.</param>
/// <param name="clock">Clock.</param>
public sealed class ImportService(ITrackRepository tracks, IWorkerClient worker, IUploadStore uploads, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Imports a YouTube or Spotify URL; the canonical URL is forwarded to the worker.</summary>
    /// <param name="url">The URL as typed.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 existing track, 202 new track, 400 invalid URL or 503 worker unavailable.</returns>
    public async Task<IResult> ImportUrlAsync(string? url, CancellationToken cancellationToken)
    {
        var parsed = SourceKey.Parse(url);
        if (!parsed.IsSuccess)
        {
            return ApiProblems.Validation("url", parsed.Error);
        }

        var existing = await tracks.FindByKeyAsync(parsed.Key, cancellationToken);
        return existing is not null ? Existing(existing) : await StartUrlAsync(parsed.Key, url!, cancellationToken);
    }

    /// <summary>Imports an uploaded file streamed from the request; the file is kept under <c>uploads/</c>.</summary>
    /// <param name="content">The file content.</param>
    /// <param name="fileName">The client file name (only used for the extension and the title).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 202, 400, 413, 415 or 503.</returns>
    public async Task<IResult> ImportUploadAsync(Stream content, string fileName, CancellationToken cancellationToken)
    {
        return UploadStore.IsAllowedExtension(Path.GetExtension(fileName))
            ? await StoreAndImportAsync(content, fileName, cancellationToken)
            : ApiProblems.UnsupportedType();
    }

    private async Task<IResult> StartUrlAsync(SourceKey key, string url, CancellationToken cancellationToken)
    {
        var request = new WorkerUrlJobRequest(key.Type, key.CanonicalUrl!);
        var track = Track.FromUrl(Guid.NewGuid(), key, url, Now);
        return (await StartAsync(track, ct => worker.CreateUrlJobAsync(request, ct), "url", cancellationToken)).Result;
    }

    private async Task<IResult> StoreAndImportAsync(Stream content, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var stored = await uploads.SaveAsync(content, Path.GetExtension(fileName), cancellationToken);
            return await ImportStoredAsync(stored, fileName, cancellationToken);
        }
        catch (Exception exception) when (exception is UploadTooLargeException or EmptyUploadException)
        {
            return exception is UploadTooLargeException
                ? ApiProblems.TooLarge()
                : ApiProblems.Validation("file", "The file is empty. Choose an MP3, WAV or FLAC file.");
        }
    }

    private async Task<IResult> ImportStoredAsync(StoredUpload stored, string fileName, CancellationToken cancellationToken)
    {
        var key = SourceKey.ForUpload(stored.Sha256);
        var existing = await tracks.FindByKeyAsync(key, cancellationToken);
        if (existing is not null)
        {
            DiscardUnlessOwned(existing, stored);
            return Existing(existing);
        }

        var track = Track.FromUpload(Guid.NewGuid(), key, new UploadedFile(fileName, stored.FileName), Now);
        var upload = new WorkerUpload(uploads.GetPath(stored.FileName), stored.FileName);
        var (result, owner) = await StartAsync(track, ct => worker.CreateUploadJobAsync(upload, ct), "file", cancellationToken);
        DiscardUnlessOwned(owner, stored);
        return result;
    }

    /// <summary>
    /// Inserts the track first (the unique index serialises parallel imports of one source, G5), then creates the
    /// worker job. On a worker rejection or outage the track is removed again, so nothing is persisted; when the call is
    /// cancelled (client gone) or fails unexpectedly the track is failed as interrupted, so it can be retried.
    /// </summary>
    private async Task<(IResult Result, Track? Owner)> StartAsync(
        Track track, Func<CancellationToken, Task<WorkerJob>> createJob, string field, CancellationToken cancellationToken)
    {
        try
        {
            await tracks.AddAsync(track, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            return await ExistingAfterRaceAsync(track.Key, cancellationToken);
        }

        return await InterruptOnFailureAsync(track, () => StartJobAsync(track, createJob, field, cancellationToken));
    }

    /// <summary>A cancelled (client gone) or unexpectedly failing job start fails the track, so it can be retried.</summary>
    private async Task<T> InterruptOnFailureAsync<T>(Track track, Func<Task<T>> start)
    {
        try
        {
            return await start();
        }
        catch
        {
            track.Fail(Sync.OrphanedImportSweeper.ImportInterrupted, Now);
            await tracks.SaveAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<(IResult Result, Track? Owner)> StartJobAsync(
        Track track, Func<CancellationToken, Task<WorkerJob>> createJob, string field, CancellationToken cancellationToken)
    {
        try
        {
            await AssignJobAsync(track, await createJob(cancellationToken), cancellationToken);
            return (TypedResults.Accepted(ApiRoutes.Track(track.Id), TrackMapper.ToDto(track)), track);
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            await tracks.RemoveAsync(track, CancellationToken.None);
            return (Rejected(exception, field), null);
        }
    }

    private async Task AssignJobAsync(Track track, WorkerJob job, CancellationToken cancellationToken)
    {
        track.AssignJob(job.JobId, Now);
        await tracks.SaveAsync(cancellationToken);
    }

    private async Task<(IResult Result, Track? Owner)> ExistingAfterRaceAsync(SourceKey key, CancellationToken cancellationToken)
    {
        var winner = await tracks.FindByKeyAsync(key, cancellationToken);
        return winner is null ? (ApiProblems.Unavailable(), null) : (Existing(winner), winner);
    }

    private void DiscardUnlessOwned(Track? owner, StoredUpload stored)
    {
        var owned = owner is { Status: not TrackStatus.Completed } && owner.UploadFileName == stored.FileName;
        if (!owned)
        {
            uploads.Delete(stored.FileName);
        }
    }

    private static Ok<TrackDto> Existing(Track track) => TypedResults.Ok(TrackMapper.ToDto(track));

    private static IResult Rejected(Exception exception, string field) => exception switch
    {
        WorkerRequestException { StatusCode: HttpStatusCode.RequestEntityTooLarge } => ApiProblems.TooLarge(),
        WorkerRequestException { StatusCode: HttpStatusCode.UnsupportedMediaType } => ApiProblems.UnsupportedType(),
        WorkerRequestException rejection => ApiProblems.Validation(field, rejection.Detail),
        _ => ApiProblems.Unavailable(),
    };
}
