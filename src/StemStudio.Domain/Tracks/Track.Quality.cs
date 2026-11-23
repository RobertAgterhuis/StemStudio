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
        throw new NotImplementedException();
    }

    /// <summary>Records the worker quality job of the queued run.</summary>
    /// <param name="workerJobId">32 hex characters.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void AssignQualityJob(string workerJobId, DateTime nowUtc)
    {
        throw new NotImplementedException();
    }

    /// <summary>Applies the worker's progress; ignored when no run is active.</summary>
    /// <param name="running">True when the worker runs the job (else it is still queued).</param>
    /// <param name="fraction">Progress 0..1.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ApplyQualityProgress(bool running, double fraction, DateTime nowUtc)
    {
        throw new NotImplementedException();
    }

    /// <summary>Makes the run ready: its stems are shown, the ratings shown so far become the previous ratings.</summary>
    /// <param name="analyzerVersion">The worker's analyzer version.</param>
    /// <param name="stems">The measures in request order.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void CompleteQuality(string analyzerVersion, IReadOnlyList<StemQuality> stems, DateTime nowUtc)
    {
        throw new NotImplementedException();
    }

    /// <summary>Fails the run; the stems of the last ready run and the previous ratings stay.</summary>
    /// <param name="error">Readable reason, or null for the default.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void FailQuality(string? error, DateTime nowUtc)
    {
        throw new NotImplementedException();
    }

    private QualityReport RequireActiveQuality() =>
        IsQualityActive ? QualityReport! : throw new InvalidOperationException($"Track {Id} has no queued or running quality report.");

    private void SetQuality(QualityReport report, DateTime nowUtc) => (QualityReport, UpdatedAt) = (report, nowUtc);
}
