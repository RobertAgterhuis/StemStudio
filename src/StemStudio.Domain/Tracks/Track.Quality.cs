using StemStudio.Domain.Insights;

namespace StemStudio.Domain.Tracks;

/// <summary>The stem quality report of a track (feature 020, data-model 5.1, 7).</summary>
public sealed partial class Track
{
    /// <summary>Error of a failed quality run without a worker text.</summary>
    public const string QualityFailedDefault = "The quality report could not be made.";

    /// <summary>The quality report; null when never made.</summary>
    public QualityReport? QualityReport { get; private set; }

    /// <summary>True while a quality run is queued or running.</summary>
    public bool IsQualityActive => QualityReport?.Status is QualityReportStatus.Queued or QualityReportStatus.Running;

    /// <summary>Queues a new quality run; the stems of the last ready run and the previous ratings stay.</summary>
    /// <param name="stemsVersion">Version of the stem files at the request (<see cref="Insights.StemsVersion"/>).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="InvalidOperationException">The track is not completed or a run is already queued or running.</exception>
    public void RequestQuality(string stemsVersion, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stemsVersion);
        if (Status != TrackStatus.Completed || IsQualityActive)
        {
            throw new InvalidOperationException($"A quality report of track {Id} cannot be started now.");
        }

        var last = QualityReport;
        SetQuality(new QualityReport
        {
            Status = QualityReportStatus.Queued,
            AnalyzerVersion = last?.AnalyzerVersion,
            MeasuredAt = last?.MeasuredAt,
            StemsVersion = last?.StemsVersion,
            RequestedStemsVersion = stemsVersion,
            Stems = last?.Stems ?? [],
            Previous = last?.Previous ?? new Dictionary<string, QualityRating>(StringComparer.Ordinal),
            RequestedAt = nowUtc,
            UpdatedAt = nowUtc,
        }, nowUtc);
    }

    /// <summary>Records the worker quality job of the queued run.</summary>
    /// <param name="workerJobId">32 hex characters.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void AssignQualityJob(string workerJobId, DateTime nowUtc)
    {
        var jobId = JobIds.Require(workerJobId, nameof(workerJobId));
        SetQuality(RequireActiveQuality() with { WorkerJobId = jobId, UpdatedAt = nowUtc }, nowUtc);
    }

    /// <summary>Applies the worker's progress; ignored when no run is active.</summary>
    /// <param name="running">True when the worker runs the job (else it is still queued).</param>
    /// <param name="fraction">Progress 0..1.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ApplyQualityProgress(bool running, double fraction, DateTime nowUtc)
    {
        if (!IsQualityActive)
        {
            return;
        }

        var report = QualityReport!;
        var percent = (int)Math.Round(Math.Clamp(fraction, 0, 1) * 100, MidpointRounding.AwayFromZero);
        var status = running ? QualityReportStatus.Running : report.Status;
        SetQuality(report with { Status = status, Progress = Math.Max(report.Progress, percent), UpdatedAt = nowUtc }, nowUtc);
    }

    /// <summary>Makes the run ready: its stems are shown, the ratings shown so far become the previous ratings.</summary>
    /// <param name="analyzerVersion">The worker's analyzer version.</param>
    /// <param name="stems">The measures in request order.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void CompleteQuality(string analyzerVersion, IReadOnlyList<StemQuality> stems, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerVersion);
        ArgumentNullException.ThrowIfNull(stems);
        var report = RequireActiveQuality();
        var previous = report.Stems.Count == 0
            ? report.Previous
            : report.Stems.GroupBy(stem => stem.Channel, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Rating, StringComparer.Ordinal);
        SetQuality(report with
        {
            Status = QualityReportStatus.Ready,
            Progress = 100,
            Error = null,
            WorkerJobId = null,
            AnalyzerVersion = analyzerVersion,
            MeasuredAt = nowUtc,
            StemsVersion = report.RequestedStemsVersion,
            Stems = stems.ToList(),
            Previous = previous,
            UpdatedAt = nowUtc,
        }, nowUtc);
    }

    /// <summary>Fails the run; the stems of the last ready run and the previous ratings stay.</summary>
    /// <param name="error">Readable reason, or null for the default.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void FailQuality(string? error, DateTime nowUtc)
    {
        var report = RequireActiveQuality();
        var reason = Text.TrimToLength(error, QualityReport.MaxErrorLength) ?? QualityFailedDefault;
        SetQuality(report with { Status = QualityReportStatus.Failed, Error = reason, WorkerJobId = null, UpdatedAt = nowUtc }, nowUtc);
    }

    private QualityReport RequireActiveQuality() =>
        IsQualityActive ? QualityReport! : throw new InvalidOperationException($"Track {Id} has no queued or running quality report.");

    private void SetQuality(QualityReport report, DateTime nowUtc) => (QualityReport, UpdatedAt) = (report, nowUtc);
}
