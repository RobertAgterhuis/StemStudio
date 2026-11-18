namespace StemStudio.Domain.Practice;

/// <summary>Status of a score view (data-model 3.5).</summary>
public enum ScoreViewStatus
{
    /// <summary>Submitted to the worker.</summary>
    Queued,

    /// <summary>The worker is rendering.</summary>
    Rendering,

    /// <summary>Pages and bars are stored.</summary>
    Ready,

    /// <summary>Rendering failed.</summary>
    Failed,
}

/// <summary>One bar box of a score view with track times.</summary>
public sealed record ScoreBar
{
    /// <summary>0-based index in document order.</summary>
    public required int Index { get; init; }

    /// <summary>Printed bar number (0 = pickup).</summary>
    public required int Number { get; init; }

    /// <summary>1-based page.</summary>
    public required int Page { get; init; }

    /// <summary>Left edge as a fraction of the page width.</summary>
    public required double X { get; init; }

    /// <summary>Top edge as a fraction of the page height.</summary>
    public required double Y { get; init; }

    /// <summary>Width as a fraction of the page width.</summary>
    public required double Width { get; init; }

    /// <summary>Height as a fraction of the page height.</summary>
    public required double Height { get; init; }

    /// <summary>Start in seconds.</summary>
    public required double StartSeconds { get; init; }

    /// <summary>End in seconds.</summary>
    public required double EndSeconds { get; init; }

    /// <summary>The same bar moved by an offset (worker times are relative to the score's input start).</summary>
    /// <param name="offsetSeconds">Offset in seconds.</param>
    /// <returns>The shifted bar.</returns>
    public ScoreBar Shifted(double offsetSeconds) => this with { StartSeconds = StartSeconds + offsetSeconds, EndSeconds = EndSeconds + offsetSeconds };
}

/// <summary>
/// The followable view of one score asset (data-model 3.5): SVG pages under <c>assets/&lt;id&gt;/view/</c> and the bar map
/// in track time.
/// </summary>
public sealed class ScoreView
{
    /// <summary>Most pages.</summary>
    public const int MaxPages = 200;

    /// <summary>Most bars.</summary>
    public const int MaxBars = 2000;

    /// <summary>Longest stored failure text.</summary>
    public const int MaxErrorLength = 500;

    /// <summary>Failure text when the worker gave none.</summary>
    public const string DefaultFailure = "The score view could not be made.";

    private ScoreView()
    {
    }

    /// <summary>The score asset (also the key).</summary>
    public Guid AssetId { get; private set; }

    /// <summary>Status.</summary>
    public ScoreViewStatus Status { get; private set; }

    /// <summary>Source track of the score (provenance), or null.</summary>
    public Guid? TrackId { get; private set; }

    /// <summary>Track time of the score's input start.</summary>
    public double OffsetSeconds { get; private set; }

    /// <summary>Stored pages.</summary>
    public int PageCount { get; private set; }

    /// <summary>+1 every time the view becomes ready (cache key of the page URLs).</summary>
    public int Revision { get; private set; }

    /// <summary>Bars in track time.</summary>
    public IReadOnlyList<ScoreBar> Bars { get; private set; } = [];

    /// <summary>Worker job while queued or rendering.</summary>
    public string? WorkerJobId { get; private set; }

    /// <summary>Failure text.</summary>
    public string? Error { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>True while queued or rendering.</summary>
    public bool IsActive => Status is ScoreViewStatus.Queued or ScoreViewStatus.Rendering;

    /// <summary>A queued view.</summary>
    /// <param name="assetId">The score asset.</param>
    /// <param name="trackId">Source track, or null.</param>
    /// <param name="offsetSeconds">Track time of the score's input start (>= 0).</param>
    /// <param name="nowUtc">Now.</param>
    /// <returns>The view (status queued, no job yet).</returns>
    public static ScoreView Create(Guid assetId, Guid? trackId, double offsetSeconds, DateTime nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offsetSeconds);
        return new ScoreView { AssetId = assetId, TrackId = trackId, OffsetSeconds = offsetSeconds, Status = ScoreViewStatus.Queued, UpdatedAt = nowUtc };
    }

    /// <summary>Records the worker job and queues the view again (also after a failure).</summary>
    /// <param name="workerJobId">Worker job id.</param>
    /// <param name="nowUtc">Now.</param>
    public void Submit(string workerJobId, DateTime nowUtc) =>
        (Status, WorkerJobId, Error, UpdatedAt) = (ScoreViewStatus.Queued, JobIds.Require(workerJobId, nameof(workerJobId)), null, nowUtc);

    /// <summary>The worker started.</summary>
    /// <param name="nowUtc">Now.</param>
    public void MarkRendering(DateTime nowUtc)
    {
        if (Status == ScoreViewStatus.Queued)
        {
            (Status, UpdatedAt) = (ScoreViewStatus.Rendering, nowUtc);
        }
    }

    /// <summary>Stores the result; bars arrive in worker time and are shifted by <see cref="OffsetSeconds"/>.</summary>
    /// <param name="pageCount">Stored pages (1..200).</param>
    /// <param name="workerBars">Bars relative to the score's input start (1..2,000).</param>
    /// <param name="nowUtc">Now.</param>
    public void Complete(int pageCount, IReadOnlyList<ScoreBar> workerBars, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(workerBars);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageCount, MaxPages);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(workerBars.Count, MaxBars);
        Bars = workerBars.Select(bar => bar.Shifted(OffsetSeconds)).ToList();
        (PageCount, Revision, Status) = (pageCount, Revision + 1, ScoreViewStatus.Ready);
        (WorkerJobId, Error, UpdatedAt) = (null, null, nowUtc);
    }

    /// <summary>The rendering failed.</summary>
    /// <param name="error">Worker text, or null.</param>
    /// <param name="nowUtc">Now.</param>
    public void Fail(string? error, DateTime nowUtc)
    {
        Error = Text.TrimToLength(error, MaxErrorLength) ?? DefaultFailure;
        (Status, WorkerJobId, UpdatedAt) = (ScoreViewStatus.Failed, null, nowUtc);
    }
}
