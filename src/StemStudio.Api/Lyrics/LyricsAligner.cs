using StemStudio.Api.Worker;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Lyrics;

/// <summary>
/// The automatic alignment rule and the submission of alignment jobs (feature 006 US2, data-model 3.2): once the
/// standard separation is completed, alignPending lyrics align at once when the worker's <c>lyricsAlignment.auto</c>
/// allows it on its device, else the estimate for that device is shown and the user starts it.
/// </summary>
/// <param name="worker">Worker client.</param>
/// <param name="applier">Records the created job.</param>
/// <param name="clock">Clock.</param>
public sealed class LyricsAligner(IWorkerClient worker, LyricsJobApplier applier, TimeProvider clock)
{
    /// <summary>The setting assumed for a worker older than contract 1.5.0.</summary>
    private static readonly WorkerLyricsAlignmentHealth OlderWorker = new() { Auto = AlignAuto.Gpu, ModelCached = false };

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Evaluates the automatic rule of alignPending lyrics whose rule is pending (not saved); waits while the track is processed.</summary>
    /// <param name="track">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached (the rule stays pending).</exception>
    public async Task EvaluateAutoAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (track.Lyrics is not { AutoAlign: true } lyrics || track.Status != TrackStatus.Completed)
        {
            return;
        }

        var (device, alignment) = await AskWorkerAsync(cancellationToken);
        await (AutoAlignRule.Applies(alignment.Auto, device, true) ? AlignNowAsync(track, lyrics, cancellationToken) : Decline(track, device, alignment.ModelCached));
    }

    private Task AlignNowAsync(Track track, TrackLyrics lyrics, CancellationToken cancellationToken)
    {
        lyrics.StartAlign(Now);
        return SubmitAsync(track, cancellationToken);
    }

    /// <summary>Records that the rule does not apply, with the estimate for the worker's device.</summary>
    private Task Decline(Track track, ProcessingDeviceKind device, bool modelCached)
    {
        var seconds = AlignmentEstimate.Seconds(track.DurationSeconds ?? 0, device, modelCached);
        track.Lyrics!.DeclineAutoAlign(seconds, AlignmentEstimate.Describe(seconds, device), Now);
        return Task.CompletedTask;
    }

    /// <summary>Submits the alignment job of aligning lyrics without one (not saved); a worker rejection fails the lyrics.</summary>
    /// <param name="track">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="WorkerUnavailableException">The worker cannot be reached.</exception>
    public async Task SubmitAsync(Track track, CancellationToken cancellationToken)
    {
        try
        {
            await CreateAsync(track, cancellationToken);
        }
        catch (WorkerRequestException rejection)
        {
            applier.ApplyRejection(track, rejection.Detail);
        }
    }

    /// <summary>Starts the alignment the user asked for (not saved): 409 before completion or on a worker rejection, 503 when unreachable.</summary>
    /// <param name="track">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Null when the job was created, else the problem (nothing should be saved).</returns>
    /// <exception cref="LyricsConflictException">The lyrics cannot be aligned in their state.</exception>
    public async Task<IResult?> StartAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (track.Status != TrackStatus.Completed)
        {
            return LyricsProblems.TrackNotReady();
        }

        track.EnsureLyrics(Now).StartAlign(Now);
        return await TryCreateAsync(track, cancellationToken);
    }

    private async Task<IResult?> TryCreateAsync(Track track, CancellationToken cancellationToken)
    {
        try
        {
            await CreateAsync(track, cancellationToken);
            return null;
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            return LyricsProblems.WorkerProblem(exception);
        }
    }

    private async Task CreateAsync(Track track, CancellationToken cancellationToken) =>
        applier.ApplySubmission(track, await worker.CreateLyricsAlignJobAsync(ToAlignRequest(track), cancellationToken));

    private async Task<(ProcessingDeviceKind Device, WorkerLyricsAlignmentHealth Alignment)> AskWorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var health = await worker.GetHealthAsync(cancellationToken);
            return (health.DeviceInfo.Kind, health.LyricsAlignment ?? OlderWorker);
        }
        catch (WorkerRequestException rejection)
        {
            throw new WorkerUnavailableException($"The processing service refused the health check: {rejection.Detail}", rejection);
        }
    }

    private static WorkerLyricsAlignRequest ToAlignRequest(Track track)
    {
        var document = track.Lyrics!.Document!;
        return new WorkerLyricsAlignRequest
        {
            StandardJobId = track.WorkerJobId!,
            Text = document.PlainText,
            Language = document.Language,
            Refine = document.SyncLevel == SyncLevel.Line ? document : null,
        };
    }
}
