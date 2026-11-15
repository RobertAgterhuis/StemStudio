using StemStudio.Api.Sync;
using StemStudio.Api.Worker;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Lyrics;

/// <summary>
/// One pass over every track whose lyrics are searching, aligning or waiting for the automatic alignment rule (plan.md
/// key flows 1-3): submits jobs that have none yet, polls the others, and deletes a completed job's worker copy once the
/// lyrics are saved (copyright minimisation). An unreachable worker keeps the state; one broken track never stops the
/// others. Logs carry the track ID and the status only, never lyrics text (FR-010).
/// </summary>
/// <param name="tracks">Track repository (scoped).</param>
/// <param name="worker">Worker client.</param>
/// <param name="applier">Applies lyrics job states.</param>
/// <param name="aligner">Evaluates the automatic alignment rule and submits alignments.</param>
/// <param name="logger">Logger.</param>
public sealed partial class LyricsSynchronizer(
    ITrackRepository tracks, IWorkerClient worker, LyricsJobApplier applier, LyricsAligner aligner, ILogger<LyricsSynchronizer> logger)
{
    /// <summary>Synchronises every active lyrics row and saves after each track.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        await TrackPass.RunAsync(
            await tracks.ListLyricsActiveAsync(cancellationToken), track => track.Id, track => SyncAsync(track, cancellationToken), (trackId, exception) => LogSyncFailed(logger, trackId, exception));
    }

    private async Task SyncAsync(Track track, CancellationToken cancellationToken)
    {
        var (before, jobId) = (track.LyricsStatus, track.Lyrics!.LyricsJobId);
        if (await TryStepAsync(track, cancellationToken) is not { } copied)
        {
            return;
        }

        await tracks.SaveAsync(cancellationToken);
        LogTransition(track, before);
        if (copied)
        {
            await applier.DeleteQuietlyAsync(jobId!, cancellationToken);
        }
    }

    /// <summary>Advances one track; null when the worker is unreachable (nothing is saved).</summary>
    private async Task<bool?> TryStepAsync(Track track, CancellationToken cancellationToken)
    {
        try
        {
            return await StepAsync(track, cancellationToken);
        }
        catch (WorkerUnavailableException)
        {
            LogWorkerUnavailable(logger, track.Id);
            return null;
        }
    }

    /// <summary>Advances one track; true when a completed job's result was copied.</summary>
    private async Task<bool> StepAsync(Track track, CancellationToken cancellationToken)
    {
        var lyrics = track.Lyrics!;
        if (lyrics.Status == LyricsStatus.AlignPending)
        {
            await aligner.EvaluateAutoAsync(track, cancellationToken);
            return false;
        }

        if (lyrics.LyricsJobId is { } jobId)
        {
            return await applier.ApplyAsync(track, await worker.GetLyricsJobAsync(jobId, cancellationToken), cancellationToken);
        }

        await (lyrics.Status == LyricsStatus.Searching ? SubmitLookupAsync(track, cancellationToken) : aligner.SubmitAsync(track, cancellationToken));
        return false;
    }

    private async Task SubmitLookupAsync(Track track, CancellationToken cancellationToken)
    {
        try
        {
            applier.ApplySubmission(track, await worker.CreateLyricsLookupJobAsync(LyricsMapper.ToLookupRequest(track), cancellationToken));
        }
        catch (WorkerRequestException rejection)
        {
            applier.ApplyRejection(track, rejection.Detail);
        }
    }

    private void LogTransition(Track track, LyricsStatus before)
    {
        if (track.LyricsStatus != before)
        {
            LogStatusChanged(logger, track.Id, before, track.LyricsStatus);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Track {TrackId}: lyrics {From} -> {To}")]
    private static partial void LogStatusChanged(ILogger logger, Guid trackId, LyricsStatus from, LyricsStatus to);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Track {TrackId}: processing service unreachable for the lyrics, retrying on the next tick")]
    private static partial void LogWorkerUnavailable(ILogger logger, Guid trackId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Track {TrackId}: lyrics synchronisation failed unexpectedly")]
    private static partial void LogSyncFailed(ILogger logger, Guid trackId, Exception exception);
}
