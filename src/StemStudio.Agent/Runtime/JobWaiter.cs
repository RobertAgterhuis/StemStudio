using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Agent.Runtime;

/// <summary>One reading of a job's state.</summary>
/// <param name="Status">running, completed, failed, cancelled or timeout.</param>
/// <param name="Progress">0-100.</param>
/// <param name="Error">Readable error, when any.</param>
public sealed record JobPoll(string Status, int Progress, string? Error)
{
    /// <summary>The job is still queued or running.</summary>
    public const string Running = "running";

    /// <summary>The job finished.</summary>
    public const string Completed = "completed";

    /// <summary>The job failed.</summary>
    public const string Failed = "failed";

    /// <summary>The job was cancelled.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>The wait exceeded MaxJobWaitMinutes.</summary>
    public const string Timeout = "timeout";

    /// <summary>Trusted message of a failed job whose own text is external (shown next to <see cref="Untrusted"/>).</summary>
    public const string FailedMessage = "The job failed.";

    /// <summary>The job's own error text (external: worker, downloader or generator output); projected only as untrusted_text.</summary>
    public string? Untrusted { get; init; }

    /// <summary>True when waiting ends.</summary>
    public bool IsTerminal => Status != Running;

    /// <summary>A failed reading with a message.</summary>
    /// <param name="error">Readable error.</param>
    /// <returns>The reading.</returns>
    public static JobPoll Fail(string error) => new(Failed, 0, error);

    /// <summary>A failed reading whose own text is external: fixed trusted message, the text as untrusted.</summary>
    /// <param name="status">Final status.</param>
    /// <param name="progress">Progress.</param>
    /// <param name="external">The job's error text.</param>
    /// <returns>The reading.</returns>
    public static JobPoll External(string status, int progress, string? external) =>
        status == Failed ? new JobPoll(status, progress, FailedMessage) { Untrusted = external } : new JobPoll(status, progress, null);

    /// <summary>The fixed message of a generation error code.</summary>
    /// <param name="code">Error code.</param>
    /// <returns>The message.</returns>
    public static string GenerationMessage(GenerationErrorCodeDto code) => code switch
    {
        GenerationErrorCodeDto.GpuRequired => "The GPU became unusable; generations need a working GPU.",
        GenerationErrorCodeDto.GpuBusy => "Not enough free GPU memory: try again when the other GPU job has finished.",
        GenerationErrorCodeDto.OutOfMemory => "The GPU ran out of memory: try a shorter range or one take.",
        GenerationErrorCodeDto.ModelDownloadFailed => "The generation model could not be downloaded.",
        GenerationErrorCodeDto.Interrupted => "The generator restarted and lost the job: retry it.",
        GenerationErrorCodeDto.InvalidInput => "The audio could not be processed.",
        GenerationErrorCodeDto.UnvoicedTarget => "The take has almost no singing to convert.",
        GenerationErrorCodeDto.PromptInvalid => "The voice sample could not be used.",
        _ => "The generation model failed.",
    };
}

/// <summary>What to wait for.</summary>
/// <param name="Job">The job.</param>
/// <param name="MaxWait">Maximum wait (MaxJobWaitMinutes).</param>
public sealed record JobWait(JobRef Job, TimeSpan MaxWait)
{
    /// <summary>Called for every non-final reading (step progress).</summary>
    public Func<JobPoll, CancellationToken, Task>? OnProgress { get; init; }
}

/// <summary>Polls a job every 2 s without model turns until it ends, the wait times out or the run is cancelled (FR-009).</summary>
/// <param name="actions">StemStudio actions.</param>
/// <param name="time">Clock.</param>
public sealed class JobWaiter(IStemStudioActions actions, TimeProvider time)
{
    /// <summary>Polling interval.</summary>
    public static TimeSpan PollInterval { get; } = TimeSpan.FromSeconds(2);

    /// <summary>Waits for the job.</summary>
    /// <param name="wait">The job and limits.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The final reading.</returns>
    public async Task<JobPoll> WaitAsync(JobWait wait, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wait);
        var started = time.GetTimestamp();
        while (true)
        {
            var poll = await JobStatusReader.ReadAsync(actions, wait.Job, cancellationToken);
            if (Final(wait, poll, started) is { } final)
            {
                return final;
            }

            await (wait.OnProgress?.Invoke(poll, cancellationToken) ?? Task.CompletedTask);
            await Task.Delay(PollInterval, time, cancellationToken);
        }
    }

    /// <summary>The terminal reading, a timeout once the maximum wait has elapsed, or null to keep polling.</summary>
    private JobPoll? Final(JobWait wait, JobPoll poll, long started) =>
        poll.IsTerminal ? poll
        : time.GetElapsedTime(started) >= wait.MaxWait ? new JobPoll(JobPoll.Timeout, poll.Progress, $"The job did not finish within {wait.MaxWait.TotalMinutes:0} minute(s).")
        : null;
}

/// <summary>Reads a job's state from the feature's endpoint (track, asset or generation; tool catalogue <c>wait_for_job</c>).</summary>
public static class JobStatusReader
{
    /// <summary>Reads the job.</summary>
    /// <param name="actions">StemStudio actions.</param>
    /// <param name="job">The job.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The reading.</returns>
    public static async Task<JobPoll> ReadAsync(IStemStudioActions actions, JobRef job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(job);
        return job.Phase switch
        {
            JobPhaseDto.Export or JobPhaseDto.Transcription => await AssetAsync(actions, job, cancellationToken),
            JobPhaseDto.Restyle or JobPhaseDto.Generation => await GenerationAsync(actions, job, cancellationToken),
            _ => await TrackAsync(actions, job, cancellationToken),
        };
    }

    private static async Task<JobPoll> TrackAsync(IStemStudioActions actions, JobRef job, CancellationToken cancellationToken)
    {
        if (job.TrackId is not { } trackId)
        {
            return JobPoll.Fail("wait_for_job needs a trackId for this phase.");
        }

        var track = await actions.Tracks.GetTrackAsync(trackId, cancellationToken);
        return job.Phase switch
        {
            JobPhaseDto.Detail => Detail(track.Detail),
            JobPhaseDto.Lyrics => Lyrics(track.LyricsStatus),
            _ => JobPoll.External(Track(track.Status), track.Progress, track.Error),
        };
    }

    private static string Track(TrackStatusDto status) => status switch
    {
        TrackStatusDto.Completed => JobPoll.Completed,
        TrackStatusDto.Failed => JobPoll.Failed,
        _ => JobPoll.Running,
    };

    private static JobPoll Detail(DetailPhaseDto detail) => detail.Status switch
    {
        DetailPhaseStatusDto.Queued or DetailPhaseStatusDto.Running => new JobPoll(JobPoll.Running, detail.Progress, null),
        DetailPhaseStatusDto.Completed => new JobPoll(JobPoll.Completed, 100, null),
        DetailPhaseStatusDto.Cancelled => new JobPoll(JobPoll.Cancelled, detail.Progress, null),
        DetailPhaseStatusDto.Failed => JobPoll.External(JobPoll.Failed, detail.Progress, detail.Error),
        _ => JobPoll.Fail("No detail separation was started for this track."),
    };

    private static JobPoll Lyrics(LyricsStatusDto status) => status switch
    {
        LyricsStatusDto.Searching or LyricsStatusDto.AlignPending or LyricsStatusDto.Aligning => new JobPoll(JobPoll.Running, 0, null),
        LyricsStatusDto.Failed => JobPoll.Fail("The lyrics job failed."),
        _ => new JobPoll(JobPoll.Completed, 100, null),
    };

    private static async Task<JobPoll> AssetAsync(IStemStudioActions actions, JobRef job, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(job.JobId, out var assetId))
        {
            return JobPoll.Fail("wait_for_job needs the jobId of the export or score.");
        }

        var asset = await actions.Assets.GetAssetAsync(assetId, cancellationToken);
        var status = asset.Status switch
        {
            AssetStatusDto.Ready => JobPoll.Completed,
            AssetStatusDto.Failed => JobPoll.Failed,
            _ => JobPoll.Running,
        };
        return JobPoll.External(status, asset.Progress, asset.Error);
    }

    private static async Task<JobPoll> GenerationAsync(IStemStudioActions actions, JobRef job, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(job.JobId, out var generationId))
        {
            return JobPoll.Fail("wait_for_job needs the jobId of the generation.");
        }

        var generation = await actions.Generations.GetGenerationAsync(generationId, cancellationToken);
        var status = Generation(generation.Status);
        return generation.Error is { } error && status == JobPoll.Failed
            ? new JobPoll(status, generation.Progress, JobPoll.GenerationMessage(error.Code)) { Untrusted = error.Message }
            : JobPoll.External(status, generation.Progress, null);
    }

    private static string Generation(GenerationStateDto status) => status switch
    {
        GenerationStateDto.Completed => JobPoll.Completed,
        GenerationStateDto.Failed => JobPoll.Failed,
        GenerationStateDto.Cancelled => JobPoll.Cancelled,
        _ => JobPoll.Running,
    };
}
