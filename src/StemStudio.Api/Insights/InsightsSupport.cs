using StemStudio.Api.SongSections;
using StemStudio.Api.Worker;
using StemStudio.Contracts;

namespace StemStudio.Api.Insights;

/// <summary>Settings of feature 020 (data-model 1, section <c>StemStudio</c>).</summary>
public sealed class InsightsOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "StemStudio";

    /// <summary>Start a quality report after every standard and detail separation (<c>StemStudio:AutoQualityReport</c>).</summary>
    public bool AutoQualityReport { get; set; } = true;

    /// <summary>Most songs in a setlist handout (<c>StemStudio:MaxSetlistHandoutSongs</c>, FR-009).</summary>
    public int MaxSetlistHandoutSongs { get; set; } = ContractLimits.MaxSetlistHandoutSongs;
}

/// <summary>
/// A lock around "check, insert and start" (LESSONS 023: a database check alone lets two requests both start). One lock for
/// the whole library: starts are rare and short.
/// </summary>
public abstract class StartLock : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Runs an action under the lock.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="action">The action.</param>
    /// <param name="cancellationToken">Cancellation of the wait.</param>
    /// <returns>The action's result.</returns>
    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the semaphore.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lock.Dispose();
        }
    }
}

/// <summary>The start lock of quality reports: check, store and start of a report run under it.</summary>
public sealed class QualityGate : StartLock;

/// <summary>
/// The start lock of handouts (security review 020, finding 4): the active limit per source, the name pick, the insert and
/// the worker submit run under it, so parallel requests neither pass the limit nor pick the same name.
/// </summary>
public sealed class HandoutGate : StartLock;

/// <summary>
/// The worker's current analyzer version (<c>/health.insights.analyzerVersion</c>) for the <c>outdated</c> flag, cached for a
/// minute; null when the worker is older than 1.11.0 or unreachable (then only the stem files decide).
/// </summary>
/// <param name="worker">The worker insights client.</param>
/// <param name="clock">Clock.</param>
public sealed class AnalyzerVersionCache(IWorkerInsightsClient worker, TimeProvider clock)
{
    /// <summary>How long a read version is kept.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private (string? Version, DateTimeOffset ReadAt)? _cached;

    /// <summary>The current analyzer version, or null when unknown.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The version or null.</returns>
    public async Task<string?> CurrentAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_cached is { } cached && clock.GetUtcNow() - cached.ReadAt < Lifetime)
            {
                return cached.Version;
            }
        }

        var version = await ReadAsync(cancellationToken);
        lock (_gate)
        {
            _cached = (version, clock.GetUtcNow());
        }

        return version;
    }

    private async Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await worker.GetInsightsHealthAsync(cancellationToken))?.AnalyzerVersion;
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            return null;
        }
    }
}

/// <summary>
/// Keeps a worker job only when its item is stored (security review 020, finding 5): the worker accepted the job, so the
/// storing steps run without the request's cancellation; when they fail the job is deleted (best effort). An invalid job id
/// (the domain refuses it with <see cref="ArgumentException"/>) answers 503 <c>worker-unavailable</c>; any other failure is
/// passed on.
/// </summary>
public static partial class SubmittedJob
{
    /// <summary>Runs the storing steps of a submitted job.</summary>
    /// <param name="store">Stores the item; its result is returned.</param>
    /// <param name="delete">Deletes the worker job (called with no cancellation).</param>
    /// <param name="logger">Logger.</param>
    /// <returns>The result of <paramref name="store"/>, or 503 for an invalid job id.</returns>
    public static async Task<IResult?> KeepAsync(Func<Task<IResult?>> store, Func<Task> delete, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(delete);
        try
        {
            return await store();
        }
        catch (Exception failure)
        {
            LogNotKept(logger, failure.Message);
            await TryDeleteAsync(delete, logger);
            if (failure is ArgumentException)
            {
                return InsightsProblems.WorkerUnavailable();
            }

            throw;
        }
    }

    private static async Task TryDeleteAsync(Func<Task> delete, ILogger logger)
    {
        try
        {
            await delete();
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException or ArgumentException)
        {
            // The worker sweeps unclaimed jobs after 24 hours; an invalid id cannot be addressed at all.
            LogNotDeleted(logger, exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A job of the processing service was accepted but its item could not be stored: {Reason}")]
    private static partial void LogNotKept(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The unclaimed job could not be removed from the processing service: {Reason}")]
    private static partial void LogNotDeleted(ILogger logger, string reason);
}

/// <summary>Problem responses of the quality and handout endpoints (backend 1.19.0 <c>QualityConflict</c>, <c>HandoutConflict</c>).</summary>
public static class InsightsProblems
{
    /// <summary>409 <c>track-not-ready</c> of a quality request.</summary>
    /// <param name="detail">Why.</param>
    /// <returns>The problem.</returns>
    public static IResult TrackNotReady(string detail) => PracticeFlowProblems.Conflict(PracticeFlowProblemTypes.TrackNotReady, "Track not ready", detail);

    /// <summary>409 <c>quality-running</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult QualityRunning() =>
        PracticeFlowProblems.Conflict(InsightsProblemTypes.QualityRunning, "Quality report already running", "A quality report for this track is already being made.");

    /// <summary>409 <c>setlist-empty</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult SetlistEmpty() => PracticeFlowProblems.Conflict("setlist-empty", "Setlist empty", "The setlist has no songs; add songs first.");

    /// <summary>409 <c>too-many-songs</c>.</summary>
    /// <param name="max">The limit.</param>
    /// <returns>The problem.</returns>
    public static IResult TooManySongs(int max) =>
        PracticeFlowProblems.Conflict(InsightsProblemTypes.TooManySongs, "Too many songs", $"A setlist handout can hold up to {max} songs.");

    /// <summary>409 <c>handouts-active</c>: the song or setlist already has the most handouts that are being made.</summary>
    /// <param name="max">The limit.</param>
    /// <returns>The problem.</returns>
    public static IResult HandoutsActive(int max) => PracticeFlowProblems.Conflict(
        InsightsProblemTypes.HandoutsActive, "Handouts are being made", $"Up to {max} handouts of a song or setlist are made at a time; wait until one is ready.");

    /// <summary>503 <c>worker-unavailable</c> (nothing was stored).</summary>
    /// <returns>The problem.</returns>
    public static IResult WorkerUnavailable() => TypedResults.Problem(
        "The processing service is not reachable. Try again in a moment.",
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Worker unavailable",
        type: PracticeFlowProblemTypes.BaseUri + "worker-unavailable");
}
