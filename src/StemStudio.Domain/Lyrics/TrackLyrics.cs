using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Lyrics;

/// <summary>
/// The lyrics of one track (feature 006, table <c>TrackLyrics</c>, data-model 3.1 and state machine 3.2). Part of the
/// <see cref="Track"/> aggregate, stored apart so lists never load the copyrighted text. A worker job is submitted by
/// the synchroniser while the status is searching or aligning without <see cref="LyricsJobId"/>.
/// </summary>
public sealed class TrackLyrics
{
    /// <summary>Reason of <see cref="LyricsStatus.NotFound"/>.</summary>
    public const string NotFoundReason = "No lyrics were found online. You can enter them yourself.";

    /// <summary>Reason of <see cref="LyricsStatus.Disabled"/>.</summary>
    public const string DisabledReason = "Online lookups are switched off, so no lyrics were searched. You can enter them yourself.";

    /// <summary>Reason prefix when the found lyrics break a document rule.</summary>
    public const string UnreadableReason = "The lyrics found online could not be read.";

    /// <summary>Reason prefix when the aligned lyrics break a document rule.</summary>
    public const string UnalignableReason = "The aligned lyrics could not be read.";

    /// <summary>Reason when the worker gives none.</summary>
    public const string FailedDefault = "The lyrics could not be processed; try again.";

    /// <summary>Longest reason.</summary>
    public const int MaxReasonLength = 1000;

    /// <summary>Largest absolute offset in milliseconds.</summary>
    public const int MaxOffsetMs = 5000;

    /// <summary>Offset step in milliseconds.</summary>
    public const int OffsetStepMs = 10;

    private TrackLyrics()
    {
    }

    /// <summary>The track (primary and foreign key).</summary>
    public Guid TrackId { get; private set; }

    /// <summary>State.</summary>
    public LyricsStatus Status { get; private set; }

    /// <summary>Readable reason (failed, notFound, disabled, the alignPending estimate).</summary>
    public string? Reason { get; private set; }

    /// <summary>The current worker lyrics job (32 lower-case hex characters).</summary>
    public string? LyricsJobId { get; private set; }

    /// <summary>The lyrics; null before the first result and after removal.</summary>
    public LyricsDocument? Document { get; private set; }

    /// <summary>User offset (-5000..5000 ms, steps of 10; positive = lyrics later).</summary>
    public int OffsetMs { get; private set; }

    /// <summary>Expected alignment seconds on the worker's device.</summary>
    public int? AlignEstimateSeconds { get; private set; }

    /// <summary>Where the alignment runs or ran.</summary>
    public JobDevice? AlignDevice { get; private set; }

    /// <summary>Alignment progress 0..100 while aligning.</summary>
    public int? AlignProgress { get; private set; }

    /// <summary>True while the automatic alignment rule still has to be evaluated (alignPending only).</summary>
    public bool AutoAlign { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>True while a search or an alignment runs.</summary>
    public bool IsBusy => Status is LyricsStatus.Searching or LyricsStatus.Aligning;

    /// <summary>True when there is text to align.</summary>
    public bool HasText => Document is { Instrumental: false, Lines.Count: > 0 };

    /// <summary>True while a line-synced document is being refined by the alignment.</summary>
    public bool IsRefining => Status == LyricsStatus.Aligning && Document?.SyncLevel == SyncLevel.Line;

    /// <summary>Creates the lyrics row of a track (status none).</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The row.</returns>
    internal static TrackLyrics Create(Guid trackId, DateTime nowUtc) => new() { TrackId = trackId, UpdatedAt = nowUtc };

    /// <summary>Starts the automatic lookup once (none -> searching); ignored in every other state (FR-001, FR-013).</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ScheduleAutomatic(DateTime nowUtc)
    {
        if (Status == LyricsStatus.None)
        {
            Search(nowUtc);
        }
    }

    /// <summary>Starts a new lookup (any idle state -> searching); the text is kept until the result replaces it (FR-009).</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="LyricsConflictException">A search or an alignment runs.</exception>
    public void Search(DateTime nowUtc)
    {
        RequireIdle();
        Become(LyricsStatus.Searching, null, nowUtc);
    }

    /// <summary>Records the worker job of a search or an alignment that has none yet.</summary>
    /// <param name="lyricsJobId">Job ID.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void AssignJob(string lyricsJobId, DateTime nowUtc)
    {
        JobIds.Require(lyricsJobId, nameof(lyricsJobId));
        if (!IsBusy || LyricsJobId is not null)
        {
            throw new InvalidOperationException($"The lyrics of track {TrackId} are {Status} and cannot take job {lyricsJobId}.");
        }

        (LyricsJobId, UpdatedAt) = (lyricsJobId, nowUtc);
    }

    /// <summary>Applies the outcome of a lookup; invalid worker data fails the lyrics (FR-002).</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="document">The document (found and instrumental).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ApplyLookup(LyricsOutcome outcome, LyricsDocument? document, DateTime nowUtc)
    {
        Require(LyricsStatus.Searching);
        switch (outcome)
        {
            case LyricsOutcome.NotFound or LyricsOutcome.Disabled:
                Document = null;
                Become(outcome == LyricsOutcome.NotFound ? LyricsStatus.NotFound : LyricsStatus.Disabled, outcome == LyricsOutcome.NotFound ? NotFoundReason : DisabledReason, nowUtc);
                break;
            default:
                AcceptLookup(outcome, document, nowUtc);
                break;
        }
    }

    /// <summary>Replaces the lyrics with text entered by the user (-> alignPending, automatic rule pending; FR-004).</summary>
    /// <param name="document">The user's plain document.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="LyricsConflictException">A search or an alignment runs.</exception>
    public void Paste(LyricsDocument document, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(document);
        RequireIdle();
        LyricsDocumentRules.Validate(document);
        Document = document;
        Become(LyricsStatus.AlignPending, null, nowUtc);
        AutoAlign = true;
    }

    /// <summary>Starts the alignment of the text (alignPending, failed with text, or found line-synced -> aligning; FR-004).</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="LyricsConflictException">Busy, no text, or already word-timed.</exception>
    public void StartAlign(DateTime nowUtc)
    {
        RequireIdle();
        if (AlignBlocker() is { } conflict)
        {
            throw new LyricsConflictException(conflict);
        }

        Become(LyricsStatus.Aligning, null, nowUtc);
        (AlignProgress, AlignDevice) = (0, null);
    }

    /// <summary>Applies a progress report of the running alignment; progress never decreases.</summary>
    /// <param name="report">The report.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ApplyAlignment(AlignmentReport report, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(report);
        Require(LyricsStatus.Aligning);
        AlignProgress = Math.Max(AlignProgress ?? 0, Math.Clamp(report.Progress, 0, 100));
        (AlignDevice, AlignEstimateSeconds, UpdatedAt) = (report.Device ?? AlignDevice, report.EstimateSeconds ?? AlignEstimateSeconds, nowUtc);
    }

    /// <summary>Applies the aligned document (aligning -> found); invalid or untimed data fails the alignment.</summary>
    /// <param name="document">The aligned document.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void CompleteAlign(LyricsDocument document, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(document);
        Require(LyricsStatus.Aligning);
        if (Invalid(document, true) is { } problem)
        {
            Fail($"{UnalignableReason} {problem}", nowUtc);
            return;
        }

        Document = WithSourceOf(Document, document);
        Become(LyricsStatus.Found, null, nowUtc);
    }

    /// <summary>Alignment times the text, it does not change where the text came from (LRCLIB credit, FR-012).</summary>
    private static LyricsDocument WithSourceOf(LyricsDocument? text, LyricsDocument aligned) => text is null
        ? aligned
        : aligned with { Source = text.Source, SourceId = text.SourceId, SourceDurationSeconds = text.SourceDurationSeconds };

    /// <summary>Fails a search or an alignment with a readable reason; the text is kept.</summary>
    /// <param name="reason">Reason (blank becomes <see cref="FailedDefault"/>, cut to 1000 characters).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void Fail(string? reason, DateTime nowUtc)
    {
        if (!IsBusy)
        {
            throw new InvalidOperationException($"The lyrics of track {TrackId} are {Status}; nothing runs.");
        }

        Become(LyricsStatus.Failed, Text.TrimToLength(reason, MaxReasonLength) ?? FailedDefault, nowUtc);
    }

    /// <summary>Cancels the running alignment: back to alignPending, or to found when a line-synced document was refined.</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="LyricsConflictException">No alignment runs.</exception>
    public void CancelAlign(DateTime nowUtc)
    {
        if (Status != LyricsStatus.Aligning)
        {
            throw new LyricsConflictException(LyricsConflict.NotAligning);
        }

        Become(IsRefining ? LyricsStatus.Found : LyricsStatus.AlignPending, null, nowUtc);
    }

    /// <summary>Removes the lyrics (any state -> removed); no automatic lookup happens any more (FR-009).</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void Remove(DateTime nowUtc)
    {
        (Document, AlignEstimateSeconds) = (null, null);
        Become(LyricsStatus.Removed, null, nowUtc);
    }

    /// <summary>Stores the user offset (FR-007).</summary>
    /// <param name="offsetMs">-5000..5000 in steps of 10.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="LyricsConflictException">There are no lyrics lines.</exception>
    /// <exception cref="DomainValidationException">The offset is out of range or not a step.</exception>
    public void SetOffset(int offsetMs, DateTime nowUtc)
    {
        if (!HasText)
        {
            throw new LyricsConflictException(LyricsConflict.None);
        }

        var errors = new ValidationErrors();
        errors.Check(Math.Abs(offsetMs) <= MaxOffsetMs && offsetMs % OffsetStepMs == 0, "offsetMs", $"Must be between -{MaxOffsetMs} and {MaxOffsetMs} in steps of {OffsetStepMs}.");
        errors.ThrowIfAny();
        (OffsetMs, UpdatedAt) = (offsetMs, nowUtc);
    }

    /// <summary>Records that the automatic alignment does not apply; the estimate is shown instead.</summary>
    /// <param name="estimateSeconds">Expected seconds on the worker's device.</param>
    /// <param name="reason">The readable estimate.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void DeclineAutoAlign(int estimateSeconds, string reason, DateTime nowUtc)
    {
        Require(LyricsStatus.AlignPending);
        (AutoAlign, AlignEstimateSeconds, Reason, UpdatedAt) = (false, estimateSeconds, Text.TrimToLength(reason, MaxReasonLength), nowUtc);
    }

    private void AcceptLookup(LyricsOutcome outcome, LyricsDocument? document, DateTime nowUtc)
    {
        if (Invalid(document, false) is { } problem)
        {
            Fail(problem.Length == 0 ? UnreadableReason : $"{UnreadableReason} {problem}", nowUtc);
            return;
        }

        Document = document;
        var status = outcome == LyricsOutcome.Instrumental ? LyricsStatus.Instrumental : StatusOf(document!);
        Become(status, null, nowUtc);
        AutoAlign = status == LyricsStatus.AlignPending;
    }

    private static LyricsStatus StatusOf(LyricsDocument document) => document.SyncLevel == SyncLevel.Plain ? LyricsStatus.AlignPending : LyricsStatus.Found;

    /// <summary>Null when the document is usable; else a description without the lyrics text (empty when it is missing).</summary>
    private static string? Invalid(LyricsDocument? document, bool timedOnly)
    {
        try
        {
            return document is null ? string.Empty : WithoutTiming(document, timedOnly);
        }
        catch (DomainValidationException invalid)
        {
            return $"({invalid.Message})";
        }
    }

    private static string? WithoutTiming(LyricsDocument document, bool timedOnly)
    {
        LyricsDocumentRules.Validate(document);
        return timedOnly && document.SyncLevel == SyncLevel.Plain ? "(no timing)" : null;
    }

    private LyricsConflict? AlignBlocker() => this switch
    {
        { HasText: false } => LyricsConflict.NoText,
        { Status: LyricsStatus.Found, Document.SyncLevel: SyncLevel.Word } => LyricsConflict.AlreadyTimed,
        _ => null, // with text and idle: alignPending, failed or found line-synced
    };

    private void RequireIdle()
    {
        if (IsBusy)
        {
            throw new LyricsConflictException(LyricsConflict.Busy);
        }
    }

    private void Require(LyricsStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"The lyrics of track {TrackId} are {Status}, not {expected}.");
        }
    }

    /// <summary>Enters a state: clears the job, the progress and the automatic rule.</summary>
    private void Become(LyricsStatus status, string? reason, DateTime nowUtc) =>
        (Status, Reason, LyricsJobId, AlignProgress, AutoAlign, UpdatedAt) = (status, reason, null, null, false, nowUtc);
}
