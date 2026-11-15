using StemStudio.Api.Storage;
using StemStudio.Api.Worker;
using StemStudio.Domain;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Sync;

/// <summary>
/// Applies one worker job state to a track: forward progress and metadata, failure, or completion (result and every
/// stem written to local storage first, then the status; data-model.md 3.1). A completed track gets its detail phase
/// queued in the same save (feature 002, FR-001); the detail synchroniser submits it.
/// </summary>
/// <param name="worker">Worker client.</param>
/// <param name="files">Stem and upload stores (the API copy of an upload is deleted after completion, U3).</param>
/// <param name="devices">Copies the job's device (feature 003).</param>
/// <param name="clock">Clock.</param>
public sealed class TrackJobApplier(IWorkerClient worker, TrackFiles files, JobDeviceApplier devices, TimeProvider clock)
{
    /// <summary>Failure text when the worker no longer knows the job (FR-009a).</summary>
    public const string InterruptedByRestart = "interrupted by restart";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Applies the job state to the track (not saved).</summary>
    /// <param name="track">An active track.</param>
    /// <param name="job">The worker job, or null when the worker answered 404.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public Task ApplyAsync(Track track, WorkerJob? job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (job is null)
        {
            return Fail(track, InterruptedByRestart);
        }

        ApplyReported(track, job);
        return job.Status switch
        {
            WorkerJobState.Completed => CompleteAsync(track, job.JobId, cancellationToken),
            WorkerJobState.Failed => Fail(track, job.Error ?? "Processing failed."),
            _ => Progress(track, job),
        };
    }

    /// <summary>Copies the device and the metadata the worker reported with any state.</summary>
    private void ApplyReported(Track track, WorkerJob job)
    {
        devices.ApplyStandard(track, job.Device);
        ApplyMetadata(track, job.Metadata);
    }

    private void ApplyMetadata(Track track, WorkerSourceMetadata? metadata)
    {
        if (metadata is not null)
        {
            track.ApplyMetadata(new TrackMetadata(metadata.Title, metadata.Artist), Now);
        }
    }

    private Task Fail(Track track, string error)
    {
        track.Fail(error, Now);
        return Task.CompletedTask;
    }

    private Task Progress(Track track, WorkerJob job)
    {
        track.ApplyProgress(new TrackProgress(Enum.Parse<TrackStatus>(job.Status.ToString()), job.Stage, job.Progress), Now);
        return Task.CompletedTask;
    }

    private async Task CompleteAsync(Track track, string jobId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await worker.GetResultAsync(jobId, cancellationToken);
            Finish(track, result, await DownloadStemsAsync(track.Id, result, cancellationToken));
        }
        catch (Exception exception) when (FailureReason(exception) is { } reason)
        {
            track.Fail(reason, Now);
        }
    }

    /// <summary>Completes the track, queues its detail phase (FR-001) and drops the API copy of the upload.</summary>
    private void Finish(Track track, WorkerJobResult result, List<Stem> stored)
    {
        track.ApplyMetadata(new TrackMetadata(result.Metadata.Title, result.Metadata.Artist), Now);
        track.Complete(new TrackCompletion(result.DurationSeconds, result.Model, stored, ToBeatGrid(result.BeatGrid)), Now);
        track.RequestDetail(Now);
        DeleteUpload(track);
    }

    private async Task<List<Stem>> DownloadStemsAsync(Guid trackId, WorkerJobResult result, CancellationToken cancellationToken)
    {
        var stored = new List<Stem>();
        foreach (var stem in result.Stems)
        {
            var size = await files.Stems.SaveStemAsync(
                trackId, stem.Name, (stream, ct) => worker.DownloadStemAsync(result.JobId, stem.Name, stream, ct), cancellationToken);
            stored.Add(new Stem(stem.Name, size));
        }

        return stored;
    }

    private void DeleteUpload(Track track)
    {
        if (track.UploadFileName is { } fileName && files.Uploads.Exists(fileName))
        {
            files.Uploads.Delete(fileName);
        }
    }

    /// <summary>A readable failure reason, or null for an unreachable worker (kept for the next tick) and unexpected errors.</summary>
    private static string? FailureReason(Exception exception) => exception switch
    {
        WorkerRequestException rejection => $"Downloading the stems failed: {rejection.Detail}",
        DomainValidationException invalid => $"The processing result is invalid: {invalid.Message}",
        IOException or UnauthorizedAccessException => "Storing the stems failed: the disk could not be written.",
        ArgumentException => "The processing result is invalid: unknown stem name.",
        _ => null,
    };

    private static BeatGrid ToBeatGrid(WorkerBeatGrid grid) => new()
    {
        Bpm = grid.Bpm,
        Beats = grid.Beats,
        Downbeats = grid.Downbeats,
        BeatsPerBar = grid.BeatsPerBar,
        Confidence = grid.Confidence,
        Reliable = grid.Reliable,
    };
}

/// <summary>The local stores a <see cref="TrackJobApplier"/> writes to.</summary>
/// <param name="Stems">Stem store.</param>
/// <param name="Uploads">Upload store.</param>
public sealed record TrackFiles(IStemFileStore Stems, IUploadStore Uploads);
