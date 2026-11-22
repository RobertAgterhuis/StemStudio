using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.InstrumentViews;

/// <summary>Why an analysis cannot start now.</summary>
public enum StartRefusal
{
    /// <summary>It may start.</summary>
    None,

    /// <summary>It is queued or running (<c>409 analysis-running</c>).</summary>
    AnalysisRunning,

    /// <summary>Ready chords have edits and the caller did not confirm (<c>409 edits-exist</c>).</summary>
    EditsExist,
}

/// <summary>Options of a run: chords <c>{vocabulary, engine}</c>, the other kinds none.</summary>
/// <param name="Vocabulary">Chord vocabulary, null for the other kinds.</param>
/// <param name="Engine">Requested chord engine (<c>btc</c>, <c>chroma-hmm</c>), null = the worker default.</param>
public sealed record AnalysisOptions(ChordVocabulary? Vocabulary, string? Engine)
{
    /// <summary>No options.</summary>
    public static AnalysisOptions Empty { get; } = new(null, null);
}

/// <summary>A submitted run.</summary>
/// <param name="JobId">Worker job id (32 lower-case hex digits).</param>
/// <param name="Options">Options of the run.</param>
/// <param name="SourceFingerprint">Fingerprint of the input stems.</param>
/// <param name="EstimateSeconds">Shown estimate, or null.</param>
public sealed record AnalysisRun(string JobId, AnalysisOptions Options, string SourceFingerprint, int? EstimateSeconds);

/// <summary>
/// One analysis of a track (data-model 7.1, table <c>TrackAnalyses</c>, key <c>(TrackId, Kind)</c>): status, current worker
/// job, options and the stored document. Transitions follow data-model 7.2.
/// </summary>
public sealed class TrackAnalysis
{
    /// <summary>Longest stored reason.</summary>
    public const int MaxReasonLength = 1000;

    /// <summary>Longest fingerprint (SHA-256 hex).</summary>
    public const int FingerprintLength = 64;

    /// <summary>Failure text when the worker gave none.</summary>
    public const string DefaultFailure = "The analysis failed.";

    /// <summary>Reason of a stale analysis.</summary>
    public const string StaleReason = "The stems changed since this analysis; it runs again.";

    private TrackAnalysis()
    {
    }

    /// <summary>The track.</summary>
    public Guid TrackId { get; private set; }

    /// <summary>The kind.</summary>
    public AnalysisKind Kind { get; private set; }

    /// <summary>Status.</summary>
    public AnalysisStatus Status { get; private set; }

    /// <summary>Readable reason for failed, unavailable and stale.</summary>
    public string? Reason { get; private set; }

    /// <summary>Current worker job while queued or running.</summary>
    public string? JobId { get; private set; }

    /// <summary>Progress 0..1.</summary>
    public double Progress { get; private set; }

    /// <summary>Options of the last run.</summary>
    public AnalysisOptions Options { get; private set; } = AnalysisOptions.Empty;

    /// <summary>The stored document (ready or stale), else null.</summary>
    public AnalysisDocument? Document { get; private set; }

    /// <summary>Engine that produced the document.</summary>
    public string? Engine { get; private set; }

    /// <summary>Device of the last run (feature 003).</summary>
    public JobDevice? Device { get; private set; }

    /// <summary>SHA-256 over the input stems of the last run.</summary>
    public string? SourceFingerprint { get; private set; }

    /// <summary>Estimate shown while queued or running.</summary>
    public int? EstimateSeconds { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>True while queued or running.</summary>
    public bool IsActive => Status is AnalysisStatus.Queued or AnalysisStatus.Running;

    /// <summary>True when the stored document has user edits (chords).</summary>
    public bool HasEdits => Document is ChordChart { HasEdits: true };

    /// <summary>An analysis that never ran (status <see cref="AnalysisStatus.None"/>).</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="nowUtc">Now.</param>
    /// <returns>The analysis.</returns>
    public static TrackAnalysis Create(Guid trackId, AnalysisKind kind, DateTime nowUtc) => new() { TrackId = trackId, Kind = kind, UpdatedAt = nowUtc };

    /// <summary>Whether a run may start now (prerequisites are checked by the caller).</summary>
    /// <param name="overwriteEdits">The user confirmed replacing edited chords.</param>
    /// <returns>The refusal, or <see cref="StartRefusal.None"/>.</returns>
    public StartRefusal CheckStart(bool overwriteEdits) => this switch
    {
        { IsActive: true } => StartRefusal.AnalysisRunning,
        { HasEdits: true } when !overwriteEdits => StartRefusal.EditsExist,
        _ => StartRefusal.None,
    };

    /// <summary>Records a submitted run (status queued, document cleared).</summary>
    /// <param name="run">The run.</param>
    /// <param name="nowUtc">Now.</param>
    public void Queue(AnalysisRun run, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(run);
        JobId = JobIds.Require(run.JobId, nameof(run));
        (Options, SourceFingerprint, EstimateSeconds) = (run.Options, run.SourceFingerprint, run.EstimateSeconds);
        (Document, Engine, Device) = (null, null, null);
        Set(AnalysisStatus.Queued, null, 0, nowUtc);
    }

    /// <summary>Applies a queued or running worker state (progress never goes back).</summary>
    /// <param name="running">The worker runs the job.</param>
    /// <param name="progress">Progress 0..1.</param>
    /// <param name="device">Reported device, or null to keep.</param>
    /// <param name="nowUtc">Now.</param>
    public void ApplyProgress(bool running, double progress, JobDevice? device, DateTime nowUtc)
    {
        if (!IsActive)
        {
            return;
        }

        Device = device ?? Device;
        var status = running ? AnalysisStatus.Running : Status;
        Set(status, null, Math.Max(Progress, Math.Clamp(progress, 0, 1)), nowUtc);
    }

    /// <summary>Stores the completed document (status ready).</summary>
    /// <param name="document">The document.</param>
    /// <param name="device">Reported device, or null to keep.</param>
    /// <param name="nowUtc">Now.</param>
    public void Complete(AnalysisDocument document, JobDevice? device, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(document);
        (Document, Engine, Device, JobId, EstimateSeconds) = (document, document.Engine, device ?? Device, null, null);
        Set(AnalysisStatus.Ready, null, 1, nowUtc);
    }

    /// <summary>The run failed or the worker lost it.</summary>
    /// <param name="reason">The worker's text, or null.</param>
    /// <param name="nowUtc">Now.</param>
    public void Fail(string? reason, DateTime nowUtc)
    {
        (JobId, EstimateSeconds) = (null, null);
        Set(AnalysisStatus.Failed, Text.TrimToLength(reason, MaxReasonLength) ?? DefaultFailure, Progress, nowUtc);
    }

    /// <summary>A prerequisite is missing: status unavailable with the reason, document cleared.</summary>
    /// <param name="reason">Readable reason ("Split the drums first.").</param>
    /// <param name="nowUtc">Now.</param>
    public void MarkUnavailable(string reason, DateTime nowUtc)
    {
        if (Status == AnalysisStatus.Unavailable && Reason == reason)
        {
            return;
        }

        (Document, Engine, JobId, EstimateSeconds) = (null, null, null, null);
        Set(AnalysisStatus.Unavailable, Text.TrimToLength(reason, MaxReasonLength), 0, nowUtc);
    }

    /// <summary>Marks a ready analysis stale when the input fingerprint changed (the document stays).</summary>
    /// <param name="fingerprint">The fingerprint of the current inputs.</param>
    /// <param name="nowUtc">Now.</param>
    /// <returns>True when the status became stale.</returns>
    public bool MarkStaleIfChanged(string fingerprint, DateTime nowUtc)
    {
        return !string.Equals(fingerprint, SourceFingerprint, StringComparison.Ordinal) && MarkStale(StaleReason, nowUtc);
    }

    /// <summary>Marks a ready analysis stale with a reason (e.g. its input stems are gone); the document stays.</summary>
    /// <param name="reason">Readable reason.</param>
    /// <param name="nowUtc">Now.</param>
    /// <returns>True when the status became stale.</returns>
    public bool MarkStale(string reason, DateTime nowUtc)
    {
        if (Status != AnalysisStatus.Ready)
        {
            return false;
        }

        Set(AnalysisStatus.Stale, Text.TrimToLength(reason, MaxReasonLength) ?? StaleReason, Progress, nowUtc);
        return true;
    }

    /// <summary>The submission could not reach the worker: back to none so a later trigger starts it again.</summary>
    /// <param name="nowUtc">Now.</param>
    public void ResetToNone(DateTime nowUtc) => Clear(nowUtc);

    /// <summary>Deleted: status none, document cleared.</summary>
    /// <param name="nowUtc">Now.</param>
    public void Clear(DateTime nowUtc)
    {
        (Document, Engine, JobId, EstimateSeconds, SourceFingerprint) = (null, null, null, null, null);
        Set(AnalysisStatus.None, null, 0, nowUtc);
    }

    /// <summary>Corrects the chord of one segment (FR-015); the chart must be ready or stale.</summary>
    /// <param name="index">0-based segment index.</param>
    /// <param name="label">Harte label.</param>
    /// <param name="nowUtc">Now.</param>
    /// <returns>False when there is no chord chart to edit (<c>409 chords-not-ready</c>).</returns>
    /// <exception cref="DomainValidationException">The index or label is invalid.</exception>
    public bool EditChord(int index, string label, DateTime nowUtc)
    {
        if (Document is not ChordChart chart || Status is not (AnalysisStatus.Ready or AnalysisStatus.Stale))
        {
            return false;
        }

        (Document, UpdatedAt) = (chart.WithEdit(index, label), nowUtc);
        return true;
    }

    private void Set(AnalysisStatus status, string? reason, double progress, DateTime nowUtc) =>
        (Status, Reason, Progress, UpdatedAt) = (status, reason, progress, nowUtc);
}
