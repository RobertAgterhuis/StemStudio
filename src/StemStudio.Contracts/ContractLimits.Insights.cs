namespace StemStudio.Contracts;

/// <summary>Limits and fixed texts of the quality report and the handouts (backend-api 1.19.0, feature 020, data-model 5-6).</summary>
public static partial class ContractLimits
{
    /// <summary>Longest practice note (FR-015).</summary>
    public const int MaxPracticeNoteLength = 4000;

    /// <summary>Most songs in a setlist handout (FR-009).</summary>
    public const int MaxSetlistHandoutSongs = 50;

    /// <summary>Most tips per stem (FR-005).</summary>
    public const int MaxTipsPerStem = 2;

    /// <summary>Fixed footer notice on every handout page (data-model 6.6; the worker rejects other texts).</summary>
    public const string HandoutNoticeFooter = "For personal practice and teaching only";

    /// <summary>Fixed full notice on the last handout page (data-model 6.6).</summary>
    public const string HandoutNoticeFull =
        "This handout was made with StemStudio for your own practice and lessons. The song, its lyrics and its music belong to "
        + "their rights holders. Do not publish, sell or share it beyond your lesson. Chords, notation and timings were made "
        + "automatically and may contain errors.";

    /// <summary>Fixed disclaimer of every quality report (<c>QualityReport.disclaimer</c> const, FR-004).</summary>
    public const string QualityDisclaimer =
        "These ratings are estimates made from the stems alone, without the original recording. They can be wrong.";
}
