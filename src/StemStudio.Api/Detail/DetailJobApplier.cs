using StemStudio.Api.Storage;
using StemStudio.Api.Sync;
using StemStudio.Api.Worker;
using StemStudio.Domain;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Detail;

/// <summary>
/// Applies one worker detail job state to a track: progress, failure, cancellation or completion (result and every
/// detail stem written to local storage first, then the status; data-model.md 3.1). No partial detail stems are ever
/// offered: a failed completion removes the track's <c>detail/</c> folder.
/// </summary>
/// <param name="worker">Worker client.</param>
/// <param name="files">Detail stem store.</param>
/// <param name="devices">Copies the detail job's device (feature 003).</param>
/// <param name="clock">Clock.</param>
public sealed class DetailJobApplier(IWorkerClient worker, IDetailStemFileStore files, JobDeviceApplier devices, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Applies the detail job state to the track (not saved).</summary>
    /// <param name="track">A track with an active detail phase.</param>
    /// <param name="job">The worker detail job, or null when the worker answered 404.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="WorkerUnavailableException">The worker became unreachable while the result was downloaded.</exception>
    public Task ApplyAsync(Track track, WorkerDetailJob? job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        ApplyDevice(track, job);
        return job?.Status switch
        {
            null => Fail(track, TrackJobApplier.InterruptedByRestart),
            WorkerDetailJobState.Completed => CompleteAsync(track, job.DetailJobId, cancellationToken),
            WorkerDetailJobState.Failed => Fail(track, job.Error ?? string.Empty),
            WorkerDetailJobState.Cancelled => Cancel(track),
            _ => Progress(track, job),
        };
    }

    /// <summary>Records the job the worker created for a pending submission, then applies its state (not saved).</summary>
    /// <param name="track">A track whose detail submission is pending.</param>
    /// <param name="job">The created (or reused) worker detail job.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public Task ApplySubmissionAsync(Track track, WorkerDetailJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(job);
        track.AssignDetailJob(job.DetailJobId, Now);
        return ApplyAsync(track, job, cancellationToken);
    }

    /// <summary>Fails a pending submission that the worker rejected (unknown standard job, stems missing).</summary>
    /// <param name="track">A track whose detail submission is pending.</param>
    /// <param name="reason">The worker's problem detail.</param>
    public void ApplyRejection(Track track, string reason)
    {
        ArgumentNullException.ThrowIfNull(track);
        track.FailDetail(reason, Now);
    }

    /// <summary>Copies the reported device; a missing job (404) keeps the device the phase has.</summary>
    private void ApplyDevice(Track track, WorkerDetailJob? job)
    {
        if (job is not null)
        {
            devices.ApplyDetail(track, job.Device);
        }
    }

    private Task Fail(Track track, string error)
    {
        track.FailDetail(error, Now);
        return Task.CompletedTask;
    }

    private Task Cancel(Track track)
    {
        track.CancelDetail(Now);
        return Task.CompletedTask;
    }

    private Task Progress(Track track, WorkerDetailJob job)
    {
        var status = job.Status == WorkerDetailJobState.Running ? DetailPhaseStatus.Running : DetailPhaseStatus.Queued;
        track.ApplyDetailProgress(new DetailProgress(status, job.Stage, job.CurrentParent, job.Progress), Now);
        return Task.CompletedTask;
    }

    private async Task CompleteAsync(Track track, string detailJobId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await worker.GetDetailResultAsync(detailJobId, cancellationToken);
            track.CompleteDetail(await StoreAllAsync(track.Id, result, cancellationToken), Now);
        }
        catch (Exception exception) when (FailureReason(exception) is { } reason)
        {
            files.DeleteAll(track.Id);
            track.FailDetail(reason, Now);
        }
    }

    private async Task<List<ParentOutcome>> StoreAllAsync(Guid trackId, WorkerDetailResult result, CancellationToken cancellationToken)
    {
        var parents = new List<ParentOutcome>();
        foreach (var parent in result.Parents)
        {
            parents.Add(await StoreAsync(trackId, result.DetailJobId, parent, cancellationToken));
        }

        return parents;
    }

    private async Task<ParentOutcome> StoreAsync(Guid trackId, string detailJobId, WorkerParentOutcome parent, CancellationToken cancellationToken)
    {
        var stems = new List<DetailStem>();
        foreach (var stem in parent.Stems)
        {
            var source = new WorkerDetailStemRef(detailJobId, parent.Parent, stem.Name);
            var size = await files.SaveAsync(
                new DetailStemKey(trackId, parent.Parent, stem.Name), (stream, ct) => worker.DownloadDetailStemAsync(source, stream, ct), cancellationToken);
            stems.Add(new DetailStem(stem.Name, size));
        }

        return new ParentOutcome(parent.Parent, Enum.Parse<ParentOutcomeKind>(parent.Outcome.ToString()), stems);
    }

    /// <summary>A readable failure reason, or null for an unreachable worker (kept for the next tick) and unexpected errors.</summary>
    private static string? FailureReason(Exception exception) => exception switch
    {
        WorkerRequestException rejection => $"Downloading the detail stems failed: {rejection.Detail}",
        DomainValidationException invalid => $"The detail result is invalid: {invalid.Message}",
        IOException or UnauthorizedAccessException => "Storing the detail stems failed: the disk could not be written.",
        ArgumentException => "The detail result is invalid: unknown detail stem name.",
        _ => null,
    };
}
