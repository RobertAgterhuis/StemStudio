namespace StemStudio.Contracts;

/// <summary>
/// Problem <c>type</c> codes of the instrument views (backend-api 1.15.0, feature 016); the full type is
/// <see cref="PracticeFlowProblemTypes.BaseUri"/> + code.
/// </summary>
public static class InstrumentViewProblemTypes
{
    /// <summary>409: the track has no completed separation yet.</summary>
    public const string TrackNotReady = "track-not-ready";

    /// <summary>409: a re-detection would replace user edits (send <c>overwriteEdits</c>).</summary>
    public const string EditsExist = "edits-exist";

    /// <summary>409: the analysis is queued or running.</summary>
    public const string AnalysisRunning = "analysis-running";

    /// <summary>409: the chord chart is not ready (edit, sheet, ChordPro, capo suggestions, <c>includeChords</c>).</summary>
    public const string ChordsNotReady = "chords-not-ready";

    /// <summary>409: drum hits need the drum detail stems.</summary>
    public const string DrumsNotSplit = "drums-not-split";

    /// <summary>409: the track has no vocals stem.</summary>
    public const string NoVocals = "no-vocals";

    /// <summary>422: tablature for a profile other than guitar or bass.</summary>
    public const string TablatureNotAllowed = "tablature-not-allowed";

    /// <summary>422: a capo for a bass tuning.</summary>
    public const string CapoNotAllowed = "capo-not-allowed";

    /// <summary>422: the tuning does not belong to the profile's instrument.</summary>
    public const string TuningMismatch = "tuning-mismatch";

    /// <summary>422: a clef other than <c>auto</c> for keys or piano.</summary>
    public const string ClefNotAllowed = "clef-not-allowed";

    /// <summary>422: a part for the drums profile.</summary>
    public const string PartNotAllowed = "part-not-allowed";
}
