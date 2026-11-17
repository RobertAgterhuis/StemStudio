namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>How much the checker reports (FR-010).</summary>
public enum Sensitivity
{
    /// <summary>Errors only.</summary>
    Loose,

    /// <summary>Errors and improvements (default).</summary>
    Balanced,

    /// <summary>Also recognised licences as ideas.</summary>
    Strict,
}

/// <summary>Category of a suggestion (data-model 4).</summary>
public enum SuggestionCategory
{
    /// <summary>Spelling.</summary>
    Spelling,

    /// <summary>Grammar.</summary>
    Grammar,

    /// <summary>Flow.</summary>
    Flow,

    /// <summary>Poetic licence.</summary>
    Licence,

    /// <summary>Device.</summary>
    Device,

    /// <summary>Alternative line.</summary>
    Alternative,

    /// <summary>Continuation lines.</summary>
    Continuation,

    /// <summary>Title idea.</summary>
    Title,
}

/// <summary>Severity of a suggestion.</summary>
public enum SuggestionSeverity
{
    /// <summary>A mistake that is not plausible as deliberate.</summary>
    Error,

    /// <summary>A possible improvement.</summary>
    Improvement,

    /// <summary>An idea (licence notes, devices, writing help).</summary>
    Idea,
}

/// <summary>What accepting a suggestion does.</summary>
public enum SuggestionOperation
{
    /// <summary>Replaces the anchor text.</summary>
    Replace,

    /// <summary>Inserts lines after the anchor line.</summary>
    InsertAfter,

    /// <summary>Sets the title.</summary>
    SetTitle,

    /// <summary>Information only.</summary>
    None,
}

/// <summary>Layer a suggestion comes from.</summary>
public enum SuggestionSource
{
    /// <summary>Hunspell dictionaries.</summary>
    Dictionary,

    /// <summary>Deterministic rules (licence lexicon, code-switch, sound spelling).</summary>
    Rules,

    /// <summary>The language model.</summary>
    Model,
}

/// <summary>Status of a suggestion (data-model 9).</summary>
public enum SuggestionStatus
{
    /// <summary>Not decided.</summary>
    Open,

    /// <summary>Applied to the text.</summary>
    Accepted,

    /// <summary>Rejected by the user.</summary>
    Rejected,

    /// <summary>Marked intentional.</summary>
    Intentional,

    /// <summary>Its anchor no longer exists.</summary>
    Stale,

    /// <summary>Another alternative of its group was accepted.</summary>
    Superseded,
}

/// <summary>Scope of an intentional mark.</summary>
public enum IntentionalScope
{
    /// <summary>One word (case-sensitive).</summary>
    Word,

    /// <summary>A phrase (exact).</summary>
    Phrase,

    /// <summary>A whole line (exact).</summary>
    Line,
}

/// <summary>Kind of assistant action.</summary>
public enum AssistKind
{
    /// <summary>Spelling, grammar, flow (and licence notes).</summary>
    Check,

    /// <summary>Devices and proposed licence.</summary>
    Devices,

    /// <summary>Alternatives for one line.</summary>
    Alternatives,

    /// <summary>Continuation lines.</summary>
    Continue,

    /// <summary>Title ideas.</summary>
    Titles,
}

/// <summary>Status of an assist.</summary>
public enum AssistStatus
{
    /// <summary>Created, not started.</summary>
    Queued,

    /// <summary>Running.</summary>
    Running,

    /// <summary>Done.</summary>
    Completed,

    /// <summary>Failed.</summary>
    Failed,

    /// <summary>Cancelled.</summary>
    Cancelled,
}

/// <summary>Who started an assist.</summary>
public enum AssistOrigin
{
    /// <summary>The editor.</summary>
    Editor,

    /// <summary>The agent.</summary>
    Agent,
}

/// <summary>Why a sheet revision was stored.</summary>
public enum RevisionCause
{
    /// <summary>The sheet was created (or existed before 023).</summary>
    Created,

    /// <summary>The user saved.</summary>
    UserSave,

    /// <summary>Suggestions were accepted.</summary>
    Accept,

    /// <summary>An undo or a restore.</summary>
    Undo,
}

/// <summary>Model-layer failure codes of an assist.</summary>
public enum AssistFailureCode
{
    /// <summary>The model backend is not reachable.</summary>
    BackendUnavailable,

    /// <summary>The Claude token budget of the assist is used up.</summary>
    BudgetExhausted,

    /// <summary>The model refused.</summary>
    Refusal,

    /// <summary>The model timed out.</summary>
    Timeout,

    /// <summary>The API restarted during the assist.</summary>
    Interrupted,

    /// <summary>Unexpected error.</summary>
    Internal,
}

/// <summary>Extension methods of the assist enums.</summary>
public static class AssistEnumExtensions
{
    /// <summary>True for completed, failed and cancelled.</summary>
    /// <param name="status">Status.</param>
    /// <returns>True when terminal.</returns>
    public static bool IsTerminal(this AssistStatus status) => status is AssistStatus.Completed or AssistStatus.Failed or AssistStatus.Cancelled;

    /// <summary>The ISO code of a language.</summary>
    /// <param name="language">Language.</param>
    /// <returns><c>nl</c> or <c>en</c>.</returns>
    public static string Code(this AssistLanguage language) => language == AssistLanguage.Dutch ? "nl" : "en";

    /// <summary>The assist language of a 010 language code.</summary>
    /// <param name="code">Language code.</param>
    /// <returns>The language, or null when the assistant does not support it.</returns>
    public static AssistLanguage? AssistLanguageOf(string? code) => code switch
    {
        "nl" => AssistLanguage.Dutch,
        "en" => AssistLanguage.English,
        _ => null,
    };
}
