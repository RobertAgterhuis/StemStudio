namespace StemStudio.Domain.Insights;

/// <summary>
/// The stem quality report of a track (data-model 5.1, stored as JSON on the track). The stems shown are those of the last
/// ready run; a new run (queued, running) or a failed run keeps them and the previous ratings.
/// </summary>
public sealed record QualityReport
{
    /// <summary>Longest stored error text.</summary>
    public const int MaxErrorLength = 500;

    /// <summary>Status of the current (or last) run.</summary>
    public required QualityReportStatus Status { get; init; }

    /// <summary>Progress 0..100 of the current run.</summary>
    public int Progress { get; init; }

    /// <summary>Failure text of the last run; null otherwise.</summary>
    public string? Error { get; init; }

    /// <summary>Worker quality job of the current run; null when none runs.</summary>
    public string? WorkerJobId { get; init; }

    /// <summary>Analyzer version of the stems shown; null when none.</summary>
    public string? AnalyzerVersion { get; init; }

    /// <summary>When the stems shown were measured (UTC); null when none.</summary>
    public DateTime? MeasuredAt { get; init; }

    /// <summary>Stem files version (<see cref="StemsVersion"/>) of the stems shown; null when none.</summary>
    public string? StemsVersion { get; init; }

    /// <summary>Stem files version at the request of the current run (moved to <see cref="StemsVersion"/> when it is ready).</summary>
    public string? RequestedStemsVersion { get; init; }

    /// <summary>The worker's measures of the last ready run.</summary>
    public IReadOnlyList<StemQuality> Stems { get; init; } = [];

    /// <summary>ChannelId -> rating of the ready report before the one shown.</summary>
    public IReadOnlyDictionary<string, QualityRating> Previous { get; init; } = new Dictionary<string, QualityRating>(StringComparer.Ordinal);

    /// <summary>When the current run was requested (UTC).</summary>
    public required DateTime RequestedAt { get; init; }

    /// <summary>Last change (UTC).</summary>
    public required DateTime UpdatedAt { get; init; }
}
