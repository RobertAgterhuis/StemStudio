namespace StemStudio.Contracts.Agent;

/// <summary>Reason codes on approval cards and in <see cref="AgentStepDto.DecisionReasons"/> (data-model 4.2).</summary>
public static class AgentDecisionReasons
{
    /// <summary>The tool is heavy (long GPU/CPU job or large disk use).</summary>
    public const string RiskHeavy = "risk-heavy";

    /// <summary>The tool deletes or overwrites without inverse.</summary>
    public const string RiskDestructive = "risk-destructive";

    /// <summary>Proposed after reading external content (the run is tainted).</summary>
    public const string Tainted = "tainted";

    /// <summary>The URL does not appear literally in a user message.</summary>
    public const string UrlNotFromUser = "url-not-from-user";

    /// <summary>Lyrics for a generated song do not come from the user's own message (010).</summary>
    public const string LyricsNotFromUser = "lyrics-not-from-user";

    /// <summary>The request imitates a named real artist or singer (009/010 guardrail; denied).</summary>
    public const string ArtistImitation = "artist-imitation";

    /// <summary>An undo compensation that deletes something not freshly created by the run.</summary>
    public const string UndoCompensation = "undo-compensation";

    /// <summary>
    /// The tool's dynamic heavy-risk check (e.g. the size of an export) failed, so the step was escalated to heavy and needs
    /// approval; never covered by a <c>plan_heavy</c> approval.
    /// </summary>
    public const string RiskCheckFailed = "risk-check-failed";

    /// <summary>
    /// <c>apply_lyric_suggestions</c> changes a lyrics sheet (1.23.0, feature 023): approval always, never covered by a plan
    /// approval, never fresh.
    /// </summary>
    public const string LyricsEdit = "lyrics-edit";
}
