using StemStudio.Api.Worker;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.InstrumentViews;

/// <summary>
/// One analysis start, delete or job application at a time: the check, the submission and the save run under this lock, so
/// two requests never start two runs of one kind and track (LESSONS backend 023).
/// </summary>
public sealed class AnalysisGate : IDisposable
{
    /// <summary>The lock.</summary>
    public SemaphoreSlim Lock { get; } = new(1, 1);

    /// <inheritdoc />
    public void Dispose() => Lock.Dispose();

    /// <summary>Runs an action under the lock.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="action">The action.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The action's result.</returns>
    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await Lock.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            Lock.Release();
        }
    }
}

/// <summary>Outcome of a start request.</summary>
public enum StartOutcome
{
    /// <summary>Queued (or stored as failed/unavailable after a refusal); the analysis is returned.</summary>
    Stored,

    /// <summary>The track does not exist.</summary>
    TrackNotFound,

    /// <summary>The standard separation has not completed.</summary>
    TrackNotReady,

    /// <summary>The drums are not split.</summary>
    DrumsNotSplit,

    /// <summary>The track has no vocal stem.</summary>
    NoVocals,

    /// <summary>A run is queued or running.</summary>
    AnalysisRunning,

    /// <summary>Ready chords have edits and the caller did not confirm.</summary>
    EditsExist,

    /// <summary>The worker cannot be reached; nothing was queued.</summary>
    WorkerUnavailable,
}

/// <summary>A stored analysis as the API shows it: the effective status of a never-run analysis whose prerequisite is missing is <c>unavailable</c>.</summary>
/// <param name="Track">The track.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Stored">The stored analysis, or null.</param>
/// <param name="Status">The effective status.</param>
/// <param name="Reason">The effective reason.</param>
public sealed record AnalysisView(Track Track, AnalysisKind Kind, TrackAnalysis? Stored, AnalysisStatus Status, string? Reason)
{
    /// <summary>The view of a stored analysis (or of none).</summary>
    /// <param name="track">The track.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="stored">The stored analysis, or null.</param>
    /// <returns>The view.</returns>
    public static AnalysisView Of(Track track, AnalysisKind kind, TrackAnalysis? stored)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (stored is { Status: not AnalysisStatus.None })
        {
            return new AnalysisView(track, kind, stored, stored.Status, stored.Reason);
        }

        var reason = AnalysisKindStrategies.UnavailableReason(AnalysisKindStrategies.Missing(kind, track));
        return new AnalysisView(track, kind, stored, reason is null ? AnalysisStatus.None : AnalysisStatus.Unavailable, reason);
    }
}

/// <summary>A start request of the API.</summary>
/// <param name="TrackId">Track id.</param>
/// <param name="Kind">Kind.</param>
/// <param name="Options">Options.</param>
/// <param name="OverwriteEdits">Replace edited chords.</param>
/// <param name="KindText">What the analysis finds, for the problem texts ("chords", "drum hits", "reference melody").</param>
public sealed record AnalysisStart(Guid TrackId, AnalysisKind Kind, AnalysisOptions Options, bool OverwriteEdits, string KindText = "analysis");

/// <summary>The result of a start request.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="View">The analysis when stored.</param>
public sealed record StartResult(StartOutcome Outcome, AnalysisView? View);

/// <summary>
/// Reads, starts, re-runs and deletes the analyses of a track (data-model 7.2-7.4): one lock around check, insert and
/// submission; an unreachable worker queues nothing; a refused job (4xx) is stored as failed with the worker's reason.
/// </summary>
/// <param name="tracks">Tracks.</param>
/// <param name="analyses">Analyses.</param>
/// <param name="worker">The worker analysis lane.</param>
/// <param name="gate">The lock.</param>
/// <param name="runs">Builds submitted runs (fingerprint and estimate).</param>
/// <param name="clock">Clock.</param>
public sealed class TrackAnalysisService(
    ITrackRepository tracks, ITrackAnalysisRepository analyses, IWorkerAnalysisClient worker, AnalysisGate gate, AnalysisRunFactory runs, TimeProvider clock)
{
    /// <summary>Start of the failure text when the worker refuses the job (4xx); the worker's detail follows.</summary>
    public const string SubmitRejected = "The processing service could not start the analysis:";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>The analysis of a track.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The view, or null when the track does not exist.</returns>
    public async Task<AnalysisView?> GetAsync(Guid trackId, AnalysisKind kind, CancellationToken cancellationToken) =>
        await tracks.FindAsync(trackId, cancellationToken) is { } track ? AnalysisView.Of(track, kind, await analyses.FindAsync(trackId, kind, cancellationToken)) : null;

    /// <summary>Starts (or re-runs) an analysis on request.</summary>
    /// <param name="request">Track, kind, options and the overwrite confirmation.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The outcome.</returns>
    public Task<StartResult> StartAsync(AnalysisStart request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return gate.RunAsync(
            async () => await tracks.FindAsync(request.TrackId, cancellationToken) is { } track
                ? await StartLockedAsync(new StartRequest(track, request.Kind, request.Options, request.OverwriteEdits), cancellationToken)
                : new StartResult(StartOutcome.TrackNotFound, null),
            cancellationToken);
    }

    /// <summary>
    /// The automatic start (data-model 7.3): only over a missing, none, unavailable or edit-free stale analysis. A missing
    /// prerequisite stores unavailable; an unreachable worker leaves the state as it was.
    /// </summary>
    /// <param name="track">The track.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when a run was queued.</returns>
    public Task<bool> StartAutomaticallyAsync(Track track, AnalysisKind kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        return gate.RunAsync(() => StartAutomaticallyLockedAsync(track, kind, cancellationToken), cancellationToken);
    }

    private async Task<bool> StartAutomaticallyLockedAsync(Track track, AnalysisKind kind, CancellationToken cancellationToken)
    {
        var stored = await analyses.FindAsync(track.Id, kind, cancellationToken);
        if (!AnalysisRules.MayStartAutomatically(stored))
        {
            return false;
        }

        var result = await StartLockedAsync(new StartRequest(track, kind, stored?.Options ?? DefaultOptions(kind), true, Automatic: true), cancellationToken);
        return result.View?.Status == AnalysisStatus.Queued;
    }

    /// <summary>Marks a ready analysis stale when its inputs changed (re-separation, detail split redone).</summary>
    /// <param name="track">The track.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when it became stale.</returns>
    public Task<bool> RefreshStalenessAsync(Track track, AnalysisKind kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        return gate.RunAsync(() => RefreshStalenessLockedAsync(track, kind, cancellationToken), cancellationToken);
    }

    private async Task<bool> RefreshStalenessLockedAsync(Track track, AnalysisKind kind, CancellationToken cancellationToken)
    {
        if (await analyses.FindAsync(track.Id, kind, cancellationToken) is not { Status: AnalysisStatus.Ready } stored
            || AnalysisKindStrategies.Plan(kind, track, stored.Options, WorkerEngines.Defaults).Plan is not { } plan)
        {
            return false;
        }

        var stale = stored.MarkStaleIfChanged(await runs.FingerprintAsync(track.Id, plan, cancellationToken), Now);
        await analyses.SaveAsync(cancellationToken);
        return stale;
    }

    /// <summary>Deletes an analysis (cancels a running worker job); status becomes none.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>False when the track does not exist.</returns>
    public Task<bool> DeleteAsync(Guid trackId, AnalysisKind kind, CancellationToken cancellationToken) =>
        gate.RunAsync(
            async () =>
            {
                if (await tracks.FindAsync(trackId, cancellationToken) is null)
                {
                    return false;
                }

                if (await analyses.FindAsync(trackId, kind, cancellationToken) is { } stored)
                {
                    await CancelQuietlyAsync(stored.JobId, cancellationToken);
                    stored.Clear(Now);
                    await analyses.SaveAsync(cancellationToken);
                }

                return true;
            },
            cancellationToken);

    /// <summary>The default options of a kind (chords: simple vocabulary, worker engine).</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The options.</returns>
    public static AnalysisOptions DefaultOptions(AnalysisKind kind) =>
        kind == AnalysisKind.Chords ? new AnalysisOptions(ChordVocabulary.Simple, null) : AnalysisOptions.Empty;

    private static StartOutcome? Refusal(MissingPrerequisite missing) => missing switch
    {
        MissingPrerequisite.TrackNotReady => StartOutcome.TrackNotReady,
        MissingPrerequisite.DrumsNotSplit => StartOutcome.DrumsNotSplit,
        MissingPrerequisite.NoVocals => StartOutcome.NoVocals,
        _ => null,
    };

    private static StartOutcome? Refusal(StartRefusal refusal) => refusal switch
    {
        StartRefusal.AnalysisRunning => StartOutcome.AnalysisRunning,
        StartRefusal.EditsExist => StartOutcome.EditsExist,
        _ => null,
    };

    private async Task<StartResult> StartLockedAsync(StartRequest request, CancellationToken cancellationToken)
    {
        var stored = await analyses.FindAsync(request.Track.Id, request.Kind, cancellationToken);
        if (stored is not null && Refusal(stored.CheckStart(request.OverwriteEdits)) is { } running)
        {
            return new StartResult(running, null);
        }

        var planned = await runs.PlanAsync(request, cancellationToken);
        return Refusal(planned.Missing) is { } refused && !request.Automatic
            ? new StartResult(refused, null)
            : await StoreAsync(request, stored, planned, cancellationToken);
    }

    /// <summary>Applies the plan to the stored (or a new) analysis and saves it, unless the outcome forbids storing.</summary>
    private async Task<StartResult> StoreAsync(StartRequest request, TrackAnalysis? stored, PlannedRun planned, CancellationToken cancellationToken)
    {
        var analysis = stored ?? TrackAnalysis.Create(request.Track.Id, request.Kind, Now);
        if (await ApplyAsync(analysis, planned, cancellationToken) is { } outcome)
        {
            return new StartResult(outcome, null);
        }

        await (stored is null ? analyses.AddAsync(analysis, cancellationToken) : analyses.SaveAsync(cancellationToken));
        return new StartResult(StartOutcome.Stored, AnalysisView.Of(request.Track, request.Kind, analysis));
    }

    /// <summary>Queues the run, or stores unavailable/failed; null when stored, an outcome when nothing may be stored.</summary>
    private async Task<StartOutcome?> ApplyAsync(TrackAnalysis analysis, PlannedRun planned, CancellationToken cancellationToken) =>
        planned.Run is { } run ? await QueueAsync(analysis, run, cancellationToken) : MarkUnavailable(analysis, planned.Missing);

    private StartOutcome? MarkUnavailable(TrackAnalysis analysis, MissingPrerequisite missing)
    {
        if (AnalysisKindStrategies.UnavailableReason(missing) is not { } reason)
        {
            return StartOutcome.TrackNotReady;
        }

        analysis.MarkUnavailable(reason, Now);
        return null;
    }

    private async Task<StartOutcome?> QueueAsync(TrackAnalysis analysis, PreparedRun run, CancellationToken cancellationToken)
    {
        try
        {
            analysis.Queue(run.ToRun((await worker.CreateAnalysisJobAsync(run.Request, cancellationToken)).AnalysisJobId), Now);
            return null;
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            return Refused(analysis, exception);
        }
    }

    /// <summary>An unreachable worker stores nothing; a refused request is stored as failed.</summary>
    private StartOutcome? Refused(TrackAnalysis analysis, Exception exception)
    {
        if (exception is not WorkerRequestException rejected)
        {
            return StartOutcome.WorkerUnavailable;
        }

        analysis.Fail($"{SubmitRejected} {rejected.Detail}", Now);
        return null;
    }

    private async Task CancelQuietlyAsync(string? jobId, CancellationToken cancellationToken)
    {
        if (jobId is null)
        {
            return;
        }

        try
        {
            await worker.DeleteAnalysisJobAsync(jobId, cancellationToken);
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            // The worker forgets unknown jobs after a restart; the analysis is cleared either way.
        }
    }
}
