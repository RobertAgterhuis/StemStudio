namespace StemStudio.Contracts;

/// <summary>
/// Problem <c>type</c> codes of the practice flow (backend-api 1.13.0, feature 014); the full type is <see cref="BaseUri"/> + code.
/// </summary>
public static class PracticeFlowProblemTypes
{
    /// <summary>Prefix of every problem type.</summary>
    public const string BaseUri = "https://stemstudio.local/problems/";

    /// <summary>409: the revision of a section replace is not the stored one ("The sections were changed in another window").</summary>
    public const string SectionsChanged = "sections-changed";

    /// <summary>409: a detection without <c>overwrite</c> after the user edited the sections.</summary>
    public const string SectionsEdited = "sections-edited";

    /// <summary>409: a detection is queued or running.</summary>
    public const string DetectionRunning = "detection-running";

    /// <summary>409: the track has no completed separation yet.</summary>
    public const string TrackNotReady = "track-not-ready";

    /// <summary>409: the asset is not a score.</summary>
    public const string NotAScore = "not-a-score";

    /// <summary>409: the score asset is not ready.</summary>
    public const string ScoreNotReady = "score-not-ready";

    /// <summary>409: the score view has no pages yet.</summary>
    public const string ScoreViewNotReady = "score-view-not-ready";

    /// <summary>409: a PDF of a setlist without entries.</summary>
    public const string SetlistEmpty = "setlist-empty";

    /// <summary>409: the practice log is switched off; nothing was stored.</summary>
    public const string PracticeLogOff = "practice-log-off";

    /// <summary>504: the setlist PDF was not ready within the timeout.</summary>
    public const string DocumentTimeout = "document-timeout";

    /// <summary>503: the setlist document job failed or its PDF is unusable (<c>SetlistDocumentUnavailable</c>, backend 1.13.0/1.14.0).</summary>
    public const string DocumentFailed = "document-failed";

    /// <summary>Field error code of <c>replaceSetlistEntries</c>: the track does not exist.</summary>
    public const string TrackNotFound = "track-not-found";

    /// <summary>Field error code of <c>replaceSetlistEntries</c>: the mix belongs to another track.</summary>
    public const string MixTrackMismatch = "mix-track-mismatch";
}
