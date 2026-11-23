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
/// The lock around "check, insert and start" of a quality report (LESSONS 023: a database check alone lets two requests both
/// start). One lock for the whole library: requests are rare and short.
/// </summary>
public sealed class QualityGate : IDisposable
{
    /// <summary>The lock.</summary>
    public SemaphoreSlim Lock { get; } = new(1, 1);

    /// <inheritdoc />
    public void Dispose() => Lock.Dispose();
}

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
        PracticeFlowProblems.Conflict("quality-running", "Quality report already running", "A quality report for this track is already being made.");

    /// <summary>409 <c>setlist-empty</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult SetlistEmpty() => PracticeFlowProblems.Conflict("setlist-empty", "Setlist empty", "The setlist has no songs; add songs first.");

    /// <summary>409 <c>too-many-songs</c>.</summary>
    /// <param name="max">The limit.</param>
    /// <returns>The problem.</returns>
    public static IResult TooManySongs(int max) =>
        PracticeFlowProblems.Conflict("too-many-songs", "Too many songs", $"A setlist handout can hold up to {max} songs.");

    /// <summary>503 <c>worker-unavailable</c> (nothing was stored).</summary>
    /// <returns>The problem.</returns>
    public static IResult WorkerUnavailable() => TypedResults.Problem(
        "The processing worker is not reachable. Try again in a moment.",
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Worker unavailable",
        type: PracticeFlowProblemTypes.BaseUri + "worker-unavailable");
}
