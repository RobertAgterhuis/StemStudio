using StemStudio.Api.Tracks;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Suggestions of a sheet (FR-006 - FR-008, FR-011): list, accept (one revision and one undo step; the server copies the stored proposals,
/// never text from the request; only open suggestions of the given sheet), reject and mark intentional.
/// </summary>
/// <param name="stores">Stores.</param>
/// <param name="changes">Sheet changes (revision, re-anchoring).</param>
/// <param name="clock">Clock.</param>
public sealed class SuggestionService(LyricAssistStores stores, LyricsSheetChanges changes, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>The suggestions of a sheet (default filter: open).</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="status">Status filter.</param>
    /// <param name="category">Category filter.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> ListAsync(Guid lyricsId, SuggestionStatusDto? status, SuggestionCategoryDto? category, CancellationToken cancellationToken)
    {
        if (await stores.Sheets.FindAsync(lyricsId, cancellationToken) is not { } sheet)
        {
            return SheetProblems.NotFound(lyricsId);
        }

        var list = await stores.Suggestions.ForSheetAsync(
            lyricsId, LyricAssistMapper.Map<SuggestionStatus>(status ?? SuggestionStatusDto.Open), category is null ? null : LyricAssistMapper.Map<SuggestionCategory>(category.Value), cancellationToken);
        return TypedResults.Ok(List(sheet, list));
    }

    /// <summary>Accepts suggestions.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="request">Ids and expected revision.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400, 404, 409 or 422.</returns>
    public Task<IResult> AcceptAsync(Guid lyricsId, SuggestionDecisionRequest? request, CancellationToken cancellationToken) =>
        WithDecisionAsync(lyricsId, request, decision => AcceptDecidedAsync(decision, cancellationToken), cancellationToken);

    /// <summary>Rejects suggestions (the text does not change).</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="request">Ids and expected revision.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400, 404 or 409.</returns>
    public Task<IResult> RejectAsync(Guid lyricsId, SuggestionDecisionRequest? request, CancellationToken cancellationToken) =>
        WithDecisionAsync(lyricsId, request, decision => RejectDecidedAsync(decision, cancellationToken), cancellationToken);

    private async Task<IResult> WithDecisionAsync(Guid lyricsId, SuggestionDecisionRequest? request, Func<Decision, Task<IResult>> then, CancellationToken cancellationToken)
    {
        var decision = await DecisionAsync(lyricsId, request, cancellationToken);
        return decision.Problem ?? await then(decision);
    }

    private async Task<IResult> AcceptDecidedAsync(Decision decision, CancellationToken cancellationToken)
    {
        var (sheet, chosen) = (decision.Sheet!, decision.Suggestions);
        var outcome = SuggestionApplier.Apply(sheet.Title, sheet.Text, [.. chosen.Where(s => s.IsOpen).Select(s => s.ToApplicable())]);
        return outcome switch
        {
            { Overlapping.Count: > 0 } => LyricAssistProblems.Suggestions(LyricAssistProblemTypes.OverlappingSuggestions, outcome.Overlapping),
            { TooLong: true } => LyricAssistProblems.TooLong(),
            _ => TypedResults.Ok(await ApplyAsync(sheet, chosen, outcome, cancellationToken)),
        };
    }

    private async Task<IResult> RejectDecidedAsync(Decision decision, CancellationToken cancellationToken)
    {
        foreach (var suggestion in decision.Suggestions.Where(s => s.IsOpen))
        {
            suggestion.Decide(SuggestionStatus.Rejected, null, Now);
        }

        await stores.Suggestions.SaveAsync(cancellationToken);
        return TypedResults.Ok(List(decision.Sheet!, decision.Suggestions));
    }

    /// <summary>Closes a correction as intentional and stores a mark for its word, phrase or line.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="suggestionId">Suggestion id.</param>
    /// <param name="request">Scope and expected revision.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201, 400, 404 or 409.</returns>
    public async Task<IResult> MarkIntentionalAsync(Guid lyricsId, Guid suggestionId, MarkSuggestionIntentionalRequest? request, CancellationToken cancellationToken)
    {
        var decision = await DecisionAsync(lyricsId, request is null ? null : new SuggestionDecisionRequest { SuggestionIds = [suggestionId], ExpectedRevision = request.ExpectedRevision }, cancellationToken);
        if (decision.Problem is { } problem)
        {
            return problem;
        }

        var (sheet, suggestion) = (decision.Sheet!, decision.Suggestions[0]);
        if (!SuggestionTypes.IsCorrection(suggestion.Category) || !suggestion.IsOpen)
        {
            return LyricAssistProblems.Suggestions(LyricAssistProblemTypes.SuggestionNotOpen, [suggestionId]);
        }

        return await MarkAsync(sheet, suggestion, request!, cancellationToken);
    }

    private async Task<IResult> MarkAsync(LyricsSheet sheet, LyricSuggestion suggestion, MarkSuggestionIntentionalRequest request, CancellationToken cancellationToken)
    {
        var scope = LyricAssistMapper.Map<IntentionalScope>(request.Scope);
        var text = scope == IntentionalScope.Line ? SheetText.Lines(sheet.Text)[suggestion.Anchor.Line - 1] : suggestion.Anchor.Original;
        IntentionalMark mark;
        try
        {
            mark = IntentionalMark.Create(Guid.NewGuid(), new MarkContent(scope, text, request.Note), Now);
        }
        catch (DomainValidationException invalid)
        {
            return ApiProblems.Validation(invalid.Errors.Select(e => e with { Field = "scope" }), string.Empty);
        }

        if (sheet.AddMark(mark) is not { } stored)
        {
            return LyricAssistProblems.MarkLimit();
        }

        suggestion.Decide(SuggestionStatus.Intentional, null, Now);
        await stores.Sheets.SaveAsync(cancellationToken);
        return TypedResults.Created(Contracts.ApiRoutes.LyricsIntentionalMark(sheet.Id, stored.Id), MarkMapper.ToDto(stored, sheet.Text));
    }

    private async Task<AcceptResultDto> ApplyAsync(LyricsSheet sheet, IReadOnlyList<LyricSuggestion> chosen, ApplyOutcome outcome, CancellationToken cancellationToken)
    {
        var group = Guid.NewGuid();
        var applied = chosen.Where(s => outcome.Applied.Contains(s.Id)).ToList();
        foreach (var suggestion in applied)
        {
            suggestion.Decide(SuggestionStatus.Accepted, group, Now);
        }

        await SupersedeAsync(sheet.Id, applied, group, cancellationToken);
        foreach (var stale in chosen.Where(s => outcome.Stale.Contains(s.Id)))
        {
            stale.Reanchor(null);
        }

        if (applied.Count > 0)
        {
            await changes.ChangeAsync(sheet, s => s.ApplyAccepted(outcome, Now), new RevisionStamp(RevisionCause.Accept, group), cancellationToken);
        }
        else
        {
            await stores.Suggestions.SaveAsync(cancellationToken);
        }

        IReadOnlyList<Guid> staleIds = [.. chosen.Where(s => s.Status == SuggestionStatus.Stale).Select(s => s.Id)];
        return await ResultAsync(sheet, (outcome.Applied, staleIds, null), cancellationToken);
    }

    private async Task SupersedeAsync(Guid sheetId, List<LyricSuggestion> applied, Guid group, CancellationToken cancellationToken)
    {
        var groups = applied.Where(s => s.GroupId is not null).Select(s => s.GroupId).ToHashSet();
        if (groups.Count == 0)
        {
            return;
        }

        foreach (var sibling in (await stores.Suggestions.ForSheetAsync(sheetId, SuggestionStatus.Open, null, cancellationToken)).Where(s => s.IsOpen && groups.Contains(s.GroupId)))
        {
            sibling.Decide(SuggestionStatus.Superseded, group, Now);
        }
    }

    /// <summary>The sheet state after an accept, undo or restore.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="ids">Applied, stale and reopened (undo) ids.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result.</returns>
    public async Task<AcceptResultDto> ResultAsync(
        LyricsSheet sheet, (IReadOnlyList<Guid> Applied, IReadOnlyList<Guid> Stale, IReadOnlyList<Guid>? Reopened) ids, CancellationToken cancellationToken)
    {
        var (applied, stale, reopened) = ids;
        ArgumentNullException.ThrowIfNull(sheet);
        return new AcceptResultDto
        {
            LyricsId = sheet.Id,
            Revision = sheet.Revision,
            Title = sheet.Title,
            Text = sheet.Text,
            Assist = await changes.InfoAsync(sheet, cancellationToken),
            Applied = applied,
            Stale = stale,
            Reopened = reopened,
        };
    }

    private async Task<Decision> DecisionAsync(Guid lyricsId, SuggestionDecisionRequest? request, CancellationToken cancellationToken)
    {
        if (await stores.Sheets.FindAsync(lyricsId, cancellationToken) is not { } sheet)
        {
            return new Decision(SheetProblems.NotFound(lyricsId), null, []);
        }

        if (request?.SuggestionIds is not { Count: > 0 and <= LyricAssistLimits.MaxDecisionIds } ids)
        {
            return new Decision(ApiProblems.Validation("suggestionIds", "Send 1 to 100 suggestion ids."), sheet, []);
        }

        if (request.ExpectedRevision != sheet.Revision)
        {
            return new Decision(LyricAssistProblems.RevisionConflict(sheet.Revision), sheet, []);
        }

        var found = await stores.Suggestions.FindManyAsync(lyricsId, [.. ids.Distinct()], cancellationToken);
        return Check(sheet, ids, found);
    }

    private static Decision Check(LyricsSheet sheet, IReadOnlyList<Guid> ids, IReadOnlyList<LyricSuggestion> found)
    {
        if (ids.Distinct().Count() != found.Count)
        {
            return new Decision(ApiProblems.NotFound("Some suggestions do not exist on this sheet."), sheet, []);
        }

        var closed = found.Where(s => s.Status is not (SuggestionStatus.Open or SuggestionStatus.Stale)).Select(s => s.Id).ToList();
        return closed.Count > 0
            ? new Decision(LyricAssistProblems.Suggestions(LyricAssistProblemTypes.SuggestionNotOpen, closed), sheet, [])
            : new Decision(null, sheet, found);
    }

    private static SuggestionListDto List(LyricsSheet sheet, IEnumerable<LyricSuggestion> suggestions) =>
        new() { LyricsId = sheet.Id, Revision = sheet.Revision, Suggestions = suggestions.Select(LyricAssistMapper.ToDto).ToList() };

    private sealed record Decision(IResult? Problem, LyricsSheet? Sheet, IReadOnlyList<LyricSuggestion> Suggestions);
}

/// <summary>Problems about sheets shared by the 023 services.</summary>
public static class SheetProblems
{
    /// <summary>404 for an unknown sheet.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <returns>The result.</returns>
    public static IResult NotFound(Guid lyricsId) => ApiProblems.NotFound($"Lyrics {lyricsId} do not exist.");
}

/// <summary>Maps intentional marks.</summary>
public static class MarkMapper
{
    /// <summary>The mark DTO.</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="text">Current sheet text.</param>
    /// <returns>The DTO.</returns>
    public static IntentionalMarkDto ToDto(IntentionalMark mark, string text)
    {
        ArgumentNullException.ThrowIfNull(mark);
        return new IntentionalMarkDto
        {
            MarkId = mark.Id,
            Scope = LyricAssistMapper.Map<IntentionalScopeDto>(mark.Scope),
            Text = mark.Text,
            Note = mark.Note,
            InText = IntentionalMarkMatcher.Occurs(mark, text),
            CreatedAt = TrackMapper.Utc(mark.CreatedAt),
        };
    }
}
