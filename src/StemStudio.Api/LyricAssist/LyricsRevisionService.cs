using StemStudio.Api.Tracks;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Undo and revisions (FR-007, data-model 2.2): undo restores the revision before the newest accept as a new revision (cause undo) and
/// reopens that accept's suggestions; after a user save it is refused with the number of changed lines; any stored revision can be
/// restored explicitly (Expert). Stale suggestions whose text is back are reopened.
/// </summary>
/// <param name="stores">Stores.</param>
/// <param name="revisions">Revisions.</param>
/// <param name="changes">Sheet changes.</param>
/// <param name="suggestions">Result builder.</param>
/// <param name="clock">Clock.</param>
public sealed class LyricsRevisionService(LyricAssistStores stores, ILyricsRevisionRepository revisions, LyricsSheetChanges changes, SuggestionService suggestions, TimeProvider clock)
{
    /// <summary>Undoes the newest accept.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="request">Expected revision.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400, 404 or 409.</returns>
    public async Task<IResult> UndoAsync(Guid lyricsId, RevisionRequest? request, CancellationToken cancellationToken)
    {
        if (await SheetAsync(lyricsId, request, cancellationToken) is { Problem: { } problem })
        {
            return problem;
        }

        var sheet = (await stores.Sheets.FindAsync(lyricsId, cancellationToken))!;
        var current = await revisions.FindAsync(sheet.Id, sheet.Revision, cancellationToken);
        var previous = await revisions.FindAsync(sheet.Id, sheet.Revision - 1, cancellationToken);
        if (!sheet.CanUndo || current?.AcceptGroupId is not { } group || previous is null)
        {
            return await RefuseUndoAsync(sheet, cancellationToken);
        }

        var accepted = (await stores.Suggestions.ForSheetAsync(sheet.Id, null, null, cancellationToken)).Where(s => s.AcceptGroupId == group).ToList();
        await changes.ChangeAsync(sheet, s => s.Restore(previous.Title, previous.Text, [.. accepted.Select(a => a.Id)], clock.GetUtcNow().UtcDateTime), RevisionCause.Undo, null, cancellationToken);
        var reopened = await ReopenAsync(sheet, accepted, cancellationToken);
        return TypedResults.Ok(await suggestions.ResultAsync(sheet, [], [], reopened, cancellationToken));
    }

    /// <summary>The stored revisions, newest first, with the lines changed against the one before.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> ListAsync(Guid lyricsId, CancellationToken cancellationToken)
    {
        if (await stores.Sheets.FindAsync(lyricsId, cancellationToken) is null)
        {
            return SheetProblems.NotFound(lyricsId);
        }

        var list = await revisions.ListAsync(lyricsId, cancellationToken);
        return TypedResults.Ok(list.Select((revision, index) => new LyricsRevisionSummaryDto
        {
            Revision = revision.Revision, Cause = LyricAssistMapper.Map<RevisionCauseDto>(revision.Cause), AcceptGroupId = revision.AcceptGroupId,
            ChangedLines = index + 1 < list.Count ? LineDiff.ChangedLines(SheetText.Lines(list[index + 1].Text), SheetText.Lines(revision.Text)) : null,
            CreatedAt = TrackMapper.Utc(revision.CreatedAt),
        }).ToList());
    }

    /// <summary>Restores a stored revision as a new revision.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="revision">The revision to restore.</param>
    /// <param name="request">Expected revision.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400, 404 or 409.</returns>
    public async Task<IResult> RestoreAsync(Guid lyricsId, int revision, RevisionRequest? request, CancellationToken cancellationToken)
    {
        if (await SheetAsync(lyricsId, request, cancellationToken) is { Problem: { } problem })
        {
            return problem;
        }

        var sheet = (await stores.Sheets.FindAsync(lyricsId, cancellationToken))!;
        if (await revisions.FindAsync(lyricsId, revision, cancellationToken) is not { } stored)
        {
            return ApiProblems.NotFound($"Revision {revision} of these lyrics is not stored.");
        }

        await changes.ChangeAsync(sheet, s => s.Restore(stored.Title, stored.Text, [], clock.GetUtcNow().UtcDateTime), RevisionCause.Undo, null, cancellationToken);
        var reopened = await ReopenAsync(sheet, [], cancellationToken);
        return TypedResults.Ok(await suggestions.ResultAsync(sheet, [], [], reopened, cancellationToken));
    }

    private async Task<IResult> RefuseUndoAsync(LyricsSheet sheet, CancellationToken cancellationToken)
    {
        var history = await revisions.ListAsync(sheet.Id, cancellationToken);
        var lastAccept = history.FirstOrDefault(r => r.Cause is RevisionCause.Accept or RevisionCause.Undo);
        return sheet.LastCause == RevisionCause.UserSave && lastAccept is { Cause: RevisionCause.Accept }
            ? LyricAssistProblems.UndoBlockedByEdit(LineDiff.ChangedLines(SheetText.Lines(lastAccept.Text), SheetText.Lines(sheet.Text)))
            : LyricAssistProblems.NothingToUndo();
    }

    // The accept's suggestions and every stale one whose anchor matches the restored text are open again.
    private async Task<IReadOnlyList<Guid>> ReopenAsync(LyricsSheet sheet, IReadOnlyList<LyricSuggestion> accepted, CancellationToken cancellationToken)
    {
        var lines = SheetText.Lines(sheet.Text);
        var stale = await stores.Suggestions.ForSheetAsync(sheet.Id, SuggestionStatus.Stale, null, cancellationToken);
        var reopened = new List<Guid>();
        foreach (var suggestion in accepted.Concat(stale).DistinctBy(s => s.Id))
        {
            if (suggestion.Anchor.IsTitle || (suggestion.Anchor.Line <= lines.Length && suggestion.Anchor.MatchesIn(lines[suggestion.Anchor.Line - 1])))
            {
                suggestion.Reopen(suggestion.Anchor);
                reopened.Add(suggestion.Id);
            }
            else if (suggestion.Status != SuggestionStatus.Stale)
            {
                suggestion.Reanchor(null);
            }
        }

        await stores.Suggestions.SaveAsync(cancellationToken);
        return reopened;
    }

    private async Task<(IResult? Problem, bool Ok)> SheetAsync(Guid lyricsId, RevisionRequest? request, CancellationToken cancellationToken)
    {
        if (await stores.Sheets.FindAsync(lyricsId, cancellationToken) is not { } sheet)
        {
            return (SheetProblems.NotFound(lyricsId), false);
        }

        if (request is null)
        {
            return (ApiProblems.Validation("expectedRevision", "Send the expected revision."), false);
        }

        return request.ExpectedRevision != sheet.Revision ? (LyricAssistProblems.RevisionConflict(sheet.Revision), false) : (null, true);
    }
}
