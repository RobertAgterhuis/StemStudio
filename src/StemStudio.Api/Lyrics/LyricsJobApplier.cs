using StemStudio.Api.Sync;
using StemStudio.Api.Worker;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Lyrics;

/// <summary>
/// Applies lyrics job states to a track (data-model 3.2): a completed lookup or alignment copies the document (the
/// domain re-validates it), failures and forgotten jobs fail the lyrics with a readable reason, a running alignment
/// reports progress, device and estimate. Nothing here logs lyrics text.
/// </summary>
/// <param name="worker">Worker client.</param>
/// <param name="devices">Validates the alignment device.</param>
/// <param name="clock">Clock.</param>
public sealed class LyricsJobApplier(IWorkerClient worker, JobDeviceApplier devices, TimeProvider clock)
{
    /// <summary>Reason when the worker no longer knows the job (restart or 24 h sweep).</summary>
    public const string ForgottenReason = "The processing service no longer knows the lyrics job; try again.";

    /// <summary>Reason when the worker cancelled the job itself.</summary>
    public const string CancelledReason = "The lyrics job was cancelled by the processing service; try again.";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Records the created worker job.</summary>
    /// <param name="track">A track whose lyrics are searching or aligning without a job.</param>
    /// <param name="job">The created job.</param>
    public void ApplySubmission(Track track, WorkerLyricsJob job)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(job);
        track.Lyrics!.AssignJob(job.LyricsJobId, Now);
    }

    /// <summary>Fails lyrics whose job the worker rejected.</summary>
    /// <param name="track">The track.</param>
    /// <param name="reason">The worker's problem detail.</param>
    public void ApplyRejection(Track track, string reason)
    {
        ArgumentNullException.ThrowIfNull(track);
        track.Lyrics!.Fail(reason, Now);
    }

    /// <summary>Applies a polled job state (not saved).</summary>
    /// <param name="track">A track whose lyrics are searching or aligning with a job.</param>
    /// <param name="job">The job, or null when the worker no longer knows it.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when the job completed and its result was read (the worker copy can be deleted after saving).</returns>
    /// <exception cref="WorkerUnavailableException">The worker became unreachable.</exception>
    public async Task<bool> ApplyAsync(Track track, WorkerLyricsJob? job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (job?.Status == WorkerLyricsJobState.Completed)
        {
            await CompleteAsync(track.Lyrics!, job.LyricsJobId, cancellationToken);
            return true;
        }

        ApplyState(track, track.Lyrics!, job);
        return false;
    }

    /// <summary>Deletes the worker copy of a job; an unreachable worker sweeps it after 24 h.</summary>
    /// <param name="lyricsJobId">Job ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task DeleteQuietlyAsync(string lyricsJobId, CancellationToken cancellationToken)
    {
        try
        {
            await worker.DeleteLyricsJobAsync(lyricsJobId, cancellationToken);
        }
        catch (WorkerUnavailableException)
        {
            // The worker removes leftovers after 24 hours.
        }
    }

    private static string? FailureOf(WorkerLyricsJob? job) => job?.Status switch
    {
        null => ForgottenReason,
        WorkerLyricsJobState.Cancelled => CancelledReason,
        _ => job.Error,
    };

    private void ApplyState(Track track, TrackLyrics lyrics, WorkerLyricsJob? job)
    {
        if (job?.Status is WorkerLyricsJobState.Queued or WorkerLyricsJobState.Running)
        {
            Progress(track, lyrics, job);
        }
        else
        {
            lyrics.Fail(FailureOf(job), Now);
        }
    }

    private void Progress(Track track, TrackLyrics lyrics, WorkerLyricsJob job)
    {
        if (lyrics.Status == LyricsStatus.Aligning)
        {
            var estimate = job.EstimatedSeconds is { } seconds ? (int)Math.Round(seconds) : (int?)null;
            lyrics.ApplyAlignment(new AlignmentReport((int)Math.Round(job.Progress * 100), devices.Convert(track.Id, job.Device), estimate), Now);
        }
    }

    private async Task CompleteAsync(TrackLyrics lyrics, string lyricsJobId, CancellationToken cancellationToken)
    {
        try
        {
            Apply(lyrics, await worker.GetLyricsResultAsync(lyricsJobId, cancellationToken));
        }
        catch (WorkerRequestException rejection)
        {
            lyrics.Fail($"Reading the lyrics failed: {rejection.Detail}", Now);
        }
    }

    private void Apply(TrackLyrics lyrics, WorkerLyricsResult result)
    {
        if (lyrics.Status == LyricsStatus.Searching)
        {
            lyrics.ApplyLookup(LyricsMapper.ToOutcome(result.Outcome), result.Lyrics, Now);
        }
        else if (result.Lyrics is { } aligned)
        {
            lyrics.CompleteAlign(aligned, Now);
        }
        else
        {
            lyrics.Fail(TrackLyrics.UnalignableReason, Now);
        }
    }
}
