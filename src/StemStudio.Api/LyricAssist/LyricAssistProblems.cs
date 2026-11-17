using Microsoft.AspNetCore.Http.HttpResults;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Problem details of the lyric assistant (backend 1.23.0 <c>AssistProblem</c>): <c>type</c> = the problem URI, <c>code</c> and the
/// extension members. Messages are fixed texts or names (track titles); sheet text is never echoed.
/// </summary>
public static class LyricAssistProblems
{
    /// <summary>Title of <c>revision-conflict</c>.</summary>
    public const string RevisionConflictTitle = "The lyrics changed since you loaded them. Reload and try again.";

    /// <summary>409 <c>revision-conflict</c> with the current revision.</summary>
    /// <param name="currentRevision">The stored revision.</param>
    /// <returns>The result.</returns>
    public static IResult RevisionConflict(int currentRevision) => Problem(
        StatusCodes.Status409Conflict, LyricAssistProblemTypes.RevisionConflict, RevisionConflictTitle,
        new() { [LyricAssistProblemMembers.CurrentRevision] = currentRevision });

    /// <summary>409 <c>language-unsupported</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult LanguageUnsupported() => Problem(
        StatusCodes.Status409Conflict, LyricAssistProblemTypes.LanguageUnsupported, "The assistant supports Dutch and English lyrics. Choose nl or en as the language of the sheet.");

    /// <summary>409 <c>not-own-lyrics</c> naming the first matching track.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="trackTitle">Track title.</param>
    /// <returns>The result.</returns>
    public static IResult NotOwnLyrics(Guid trackId, string trackTitle) => Problem(
        StatusCodes.Status409Conflict, LyricAssistProblemTypes.NotOwnLyrics, $"This text matches the lyrics of \"{trackTitle}\". The assistant works on your own lyrics only.",
        new() { [LyricAssistProblemMembers.MatchedTrackTitle] = trackTitle, [LyricAssistProblemMembers.MatchedTrackId] = trackId, [LyricAssistProblemMembers.Overridable] = true });

    /// <summary>400 <c>imitation</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult Imitation() => TypedResults.Problem(
        type: LyricAssistProblemTypes.Uri(LyricAssistProblemTypes.Imitation), statusCode: StatusCodes.Status400BadRequest,
        title: "Writing like a real artist is not supported. Describe a mood, theme or style instead.");

    /// <summary>409 <c>assist-active</c>.</summary>
    /// <param name="activeAssistId">The running assist.</param>
    /// <returns>The result.</returns>
    public static IResult AssistActive(Guid activeAssistId) => Problem(
        StatusCodes.Status409Conflict, LyricAssistProblemTypes.AssistActive, "A check is running. Wait for it or cancel it.",
        new() { [LyricAssistProblemMembers.ActiveAssistId] = activeAssistId });

    /// <summary>409 <c>model-unavailable</c>.</summary>
    /// <param name="reason">Why.</param>
    /// <param name="fix">How to fix.</param>
    /// <returns>The result.</returns>
    public static IResult ModelUnavailable(string? reason, string? fix) => Problem(
        StatusCodes.Status409Conflict, LyricAssistProblemTypes.ModelUnavailable, "No language model is available for this action.",
        new() { [LyricAssistProblemMembers.Reason] = reason, [LyricAssistProblemMembers.Fix] = fix });

    /// <summary>409 <c>claude-notice-required</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult ClaudeNoticeRequired() => Problem(
        StatusCodes.Status409Conflict, LyricAssistProblemTypes.ClaudeNoticeRequired, "With Claude selected, acknowledge the lyric-assistant notice first: the sheet text is sent to Anthropic.");

    /// <summary>409 <c>overlapping-suggestions</c> or <c>suggestion-not-open</c> with the ids.</summary>
    /// <param name="code">The code.</param>
    /// <param name="ids">The suggestions.</param>
    /// <returns>The result.</returns>
    public static IResult Suggestions(string code, IReadOnlyList<Guid> ids) => Problem(
        StatusCodes.Status409Conflict, code,
        code == LyricAssistProblemTypes.OverlappingSuggestions ? "Some suggestions change the same text; accept them one at a time." : "Some suggestions are no longer open.",
        new() { [LyricAssistProblemMembers.SuggestionIds] = ids });

    /// <summary>409 <c>nothing-to-undo</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult NothingToUndo() => Problem(StatusCodes.Status409Conflict, LyricAssistProblemTypes.NothingToUndo, "The newest change is not an accepted suggestion; there is nothing to undo.");

    /// <summary>409 <c>undo-blocked-by-edit</c>.</summary>
    /// <param name="changedLines">Lines changed by the user since the accept.</param>
    /// <returns>The result.</returns>
    public static IResult UndoBlockedByEdit(int changedLines) => Problem(
        StatusCodes.Status409Conflict, LyricAssistProblemTypes.UndoBlockedByEdit,
        $"You saved other edits after accepting ({changedLines} changed lines). Undo would lose them; use the revision list instead.",
        new() { [LyricAssistProblemMembers.ChangedLines] = changedLines });

    /// <summary>409 <c>override-not-applicable</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult OverrideNotApplicable() => Problem(
        StatusCodes.Status409Conflict, LyricAssistProblemTypes.OverrideNotApplicable, "The sheet does not match the lyrics of this track, so there is nothing to confirm.");

    /// <summary>409 <c>statement-outdated</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult StatementOutdated() => Problem(
        StatusCodes.Status409Conflict, LyricAssistProblemTypes.StatementOutdated, "The own-work statement changed. Read the current statement and confirm again.");

    /// <summary>409 <c>assist-ended</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult AssistEnded() => Simple(StatusCodes.Status409Conflict, LyricAssistProblemTypes.AssistEnded, "The assist has already ended.");

    /// <summary>422 <c>too-long</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult TooLong() => Simple(StatusCodes.Status422UnprocessableEntity, LyricAssistProblemTypes.TooLong, "The result would be longer than 4,096 characters (or the title longer than 80).");

    /// <summary>409 <c>mark-limit</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult MarkLimit() => Simple(StatusCodes.Status409Conflict, LyricAssistProblemTypes.MarkLimit, "The sheet already has 200 intentional marks.");

    /// <summary>409 <c>word-limit</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult WordLimit() => Simple(StatusCodes.Status409Conflict, LyricAssistProblemTypes.WordLimit, "Your word list of this language already has 5,000 words.");

    private static ProblemHttpResult Simple(int status, string code, string title) =>
        TypedResults.Problem(type: LyricAssistProblemTypes.Uri(code), statusCode: status, title: title);

    private static ProblemHttpResult Problem(int status, string code, string title, Dictionary<string, object?>? extensions = null)
    {
        var members = new Dictionary<string, object?> { [LyricAssistProblemMembers.Code] = code };
        foreach (var (key, value) in extensions ?? [])
        {
            members[key] = value;
        }

        return TypedResults.Problem(type: LyricAssistProblemTypes.Uri(code), statusCode: status, title: title, extensions: members);
    }
}
