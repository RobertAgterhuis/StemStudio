namespace StemStudio.Contracts.LyricAssist;

/// <summary>
/// Problem codes of the lyric-assistant endpoints. <c>type</c> = <c>https://stemstudio.local/problems/&lt;code&gt;</c>; the codes of
/// schema <c>AssistProblem</c> also travel in its <c>code</c> extension member (<see cref="AssistProblemCodes"/>).
/// </summary>
public static class LyricAssistProblemTypes
{
    /// <summary>Base URI of every problem type.</summary>
    public const string BaseUri = "https://stemstudio.local/problems/";

    /// <summary>409: the sheet changed since <c>expectedRevision</c>; extension <c>currentRevision</c>.</summary>
    public const string RevisionConflict = "revision-conflict";

    /// <summary>409: the sheet language is not <c>nl</c> or <c>en</c>.</summary>
    public const string LanguageUnsupported = "language-unsupported";

    /// <summary>409: the sheet matches library track lyrics; extensions <c>matchedTrackTitle</c>, <c>matchedTrackId</c>, <c>overridable</c>.</summary>
    public const string NotOwnLyrics = "not-own-lyrics";

    /// <summary>409: another assist runs (one globally); extension <c>activeAssistId</c>.</summary>
    public const string AssistActive = "assist-active";

    /// <summary>409: the kind needs a model and none is usable; extensions <c>reason</c>, <c>fix</c>.</summary>
    public const string ModelUnavailable = "model-unavailable";

    /// <summary>409: Claude is selected and the lyric-assistant notice is not acknowledged.</summary>
    public const string ClaudeNoticeRequired = "claude-notice-required";

    /// <summary>409: the accepted suggestions overlap; extension <c>suggestionIds</c>.</summary>
    public const string OverlappingSuggestions = "overlapping-suggestions";

    /// <summary>409: a suggestion is not open; extension <c>suggestionIds</c>.</summary>
    public const string SuggestionNotOpen = "suggestion-not-open";

    /// <summary>409: the newest revision is not an accept.</summary>
    public const string NothingToUndo = "nothing-to-undo";

    /// <summary>409: user edits were saved after the accept; extension <c>changedLines</c>.</summary>
    public const string UndoBlockedByEdit = "undo-blocked-by-edit";

    /// <summary>409: the sheet does not currently match <c>matchedTrackId</c> (or matches nothing).</summary>
    public const string OverrideNotApplicable = "override-not-applicable";

    /// <summary>409: <c>statementVersion</c> is not the current own-work statement.</summary>
    public const string StatementOutdated = "statement-outdated";

    /// <summary>400: the instruction names an artist or asks to write like someone (ADR-018 D8, ADR-037 D7).</summary>
    public const string Imitation = "imitation";

    /// <summary>409: the assist already completed, failed or was cancelled.</summary>
    public const string AssistEnded = "assist-ended";

    /// <summary>422: the result would exceed 4,096 characters after normalisation.</summary>
    public const string TooLong = "too-long";

    /// <summary>409: the sheet already has 200 intentional marks.</summary>
    public const string MarkLimit = "mark-limit";

    /// <summary>409: the personal word list of this language has 5,000 words.</summary>
    public const string WordLimit = "word-limit";

    /// <summary>The values of <c>AssistProblem.code</c>, in contract order.</summary>
    public static IReadOnlyList<string> AssistProblemCodes { get; } =
        [RevisionConflict, LanguageUnsupported, NotOwnLyrics, AssistActive, ModelUnavailable, ClaudeNoticeRequired, OverlappingSuggestions, SuggestionNotOpen, NothingToUndo, UndoBlockedByEdit, OverrideNotApplicable, StatementOutdated];

    /// <summary>The full <c>type</c> URI of a problem code.</summary>
    /// <param name="code">A code of this class.</param>
    /// <returns>The problem type URI.</returns>
    public static string Uri(string code) => BaseUri + code;
}
