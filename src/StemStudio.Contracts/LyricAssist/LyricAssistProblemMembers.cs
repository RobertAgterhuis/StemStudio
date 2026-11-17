namespace StemStudio.Contracts.LyricAssist;

/// <summary>Extension member names of schema <c>AssistProblem</c> (problem details of the lyric assistant).</summary>
public static class LyricAssistProblemMembers
{
    /// <summary>The problem code (<see cref="LyricAssistProblemTypes.AssistProblemCodes"/>).</summary>
    public const string Code = "code";

    /// <summary>Current sheet revision (<c>revision-conflict</c>).</summary>
    public const string CurrentRevision = "currentRevision";

    /// <summary>Title of the matching track (<c>not-own-lyrics</c>, at most 200 characters).</summary>
    public const string MatchedTrackTitle = "matchedTrackTitle";

    /// <summary>First matching track not covered by an override (<c>not-own-lyrics</c>).</summary>
    public const string MatchedTrackId = "matchedTrackId";

    /// <summary>True: the editor offers the own-work confirmation (<c>not-own-lyrics</c>).</summary>
    public const string Overridable = "overridable";

    /// <summary>The running assist (<c>assist-active</c>).</summary>
    public const string ActiveAssistId = "activeAssistId";

    /// <summary>Readable reason (<c>model-unavailable</c>).</summary>
    public const string Reason = "reason";

    /// <summary>Readable fix (<c>model-unavailable</c>).</summary>
    public const string Fix = "fix";

    /// <summary>Affected suggestions (<c>overlapping-suggestions</c>, <c>suggestion-not-open</c>; at most 100).</summary>
    public const string SuggestionIds = "suggestionIds";

    /// <summary>Lines changed by user edits after the accept (<c>undo-blocked-by-edit</c>).</summary>
    public const string ChangedLines = "changedLines";
}
