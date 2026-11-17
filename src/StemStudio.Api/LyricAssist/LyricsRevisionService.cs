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
        var (problem, sheet) = await SheetAsync(lyricsId, request, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var current = await revisions.FindAsync(sheet.Id, sheet.Revision, cancellationToken);
        var previous = await revisions.FindAsync(sheet.Id, sheet.Revision - 1, cancellationToken);
        if (!sheet.CanUndo || current?.AcceptGroupId is not { } group || previous is null)
        {
            return await RefuseUndoAsync(sheet, cancellationToken);
        }

        var accepted = (await stores.Suggestions.ForSheetAsync(sheet.Id, null, null, cancellationToken)).Where(s => s.AcceptGroupId == group).ToList();
        return await RestoreToAsync(sheet, previous, accepted, cancellationToken);
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
        var (problem, sheet) = await SheetAsync(lyricsId, request, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        return await revisions.FindAsync(lyricsId, revision, cancellationToken) is { } stored
            ? await RestoreToAsync(sheet, stored, [], cancellationToken)
            : ApiProblems.NotFound($"Revision {revision} of these lyrics is not stored.");
    }

    // Restores a stored revision as a new revision (cause undo) and reopens the given and the stale suggestions that fit it again.
    private async Task<IResult> RestoreToAsync(LyricsSheet sheet, LyricsSheetRevision target, IReadOnlyList<LyricSuggestion> accepted, CancellationToken cancellationToken)
    {
        var removed = accepted.Select(a => a.Id).ToList();
        await changes.ChangeAsync(sheet, s => s.Restore(target.Title, target.Text, removed, clock.GetUtcNow().UtcDateTime), new RevisionStamp(RevisionCause.Undo), cancellationToken);
        var reopened = await ReopenAsync(sheet, accepted, cancellationToken);
        return TypedResults.Ok(await suggestions.ResultAsync(sheet, ([], [], reopened), cancellationToken));
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

    // The tracked sheet, or the problem (404, 400 without a request, 409 revision-conflict); the sheet is set whenever the problem is null.
    private async Task<(IResult? Problem, LyricsSheet Sheet)> SheetAsync(Guid lyricsId, RevisionRequest? request, CancellationToken cancellationToken)
    {
        if (await stores.Sheets.FindAsync(lyricsId, cancellationToken) is not { } sheet)
        {
            return (SheetProblems.NotFound(lyricsId), null!);
        }

        if (request is null)
        {
            return (ApiProblems.Validation("expectedRevision", "Send the expected revision."), sheet);
        }

        return (request.ExpectedRevision != sheet.Revision ? LyricAssistProblems.RevisionConflict(sheet.Revision) : null, sheet);
    }
}
