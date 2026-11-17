using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>Decisions on suggestions (FR-006 - FR-008, FR-011, FR-012): accept (one revision, one undo step), reject, mark as
/// intentional, add to my words, undo; a stale revision reloads the sheet and the suggestions.</summary>
public sealed partial class LyricAssistSession
{
    /// <summary>The default intentional scope: word for spelling, phrase otherwise (ui-test-ids keyboard Alt+I).</summary>
    /// <param name="s">The suggestion.</param>
    /// <returns>The scope.</returns>
    public static IntentionalScopeDto DefaultScope(LyricSuggestionDto s) =>
        s.Category == SuggestionCategoryDto.Spelling ? IntentionalScopeDto.Word : IntentionalScopeDto.Phrase;

    /// <summary>True for corrections (they can be marked as intentional).</summary>
    /// <param name="s">The suggestion.</param>
    /// <returns>Whether it is a correction.</returns>
    public static bool IsCorrection(LyricSuggestionDto s) => s.Category is SuggestionCategoryDto.Spelling or SuggestionCategoryDto.Grammar or SuggestionCategoryDto.Flow;

    /// <summary>Accepts suggestions (saves unsaved changes first).</summary>
    /// <param name="ids">The suggestions.</param>
    /// <returns>A task that completes when decided.</returns>
    public async Task AcceptAsync(IReadOnlyList<Guid> ids)
    {
        if (ids.Count > 0 && await SaveIfDirtyAsync())
        {
            await DecideAsync(async () =>
            {
                var result = await api.AcceptLyricSuggestionsAsync(Document.Id, Decision(ids));
                Document.Apply(result);
                Live = AcceptedText(result);
            });
        }
    }

    /// <summary>Accepts every visible replacement ("Accept all" for the filter).</summary>
    /// <returns>A task that completes when decided.</returns>
    public Task AcceptAllAsync() => AcceptAsync(Board.AcceptAllIds);

    /// <summary>Rejects one suggestion.</summary>
    /// <param name="id">The suggestion.</param>
    /// <returns>A task that completes when decided.</returns>
    public Task RejectAsync(Guid id) => DecideAsync(async () =>
    {
        Board.Merge((await api.RejectLyricSuggestionsAsync(Document.Id, Decision([id]))).Suggestions);
        Live = "Suggestion rejected";
    });

    /// <summary>Marks the text of a correction as intentional.</summary>
    /// <param name="id">The suggestion.</param>
    /// <param name="scope">Word, phrase or line.</param>
    /// <returns>A task that completes when marked.</returns>
    public Task MarkIntentionalAsync(Guid id, IntentionalScopeDto scope) => DecideAsync(async () =>
    {
        var body = new MarkSuggestionIntentionalRequest { Scope = scope, ExpectedRevision = Document.Revision };
        var mark = await api.MarkSuggestionIntentionalAsync(Document.Id, id, body);
        Live = $"Marked as intentional: {mark.Text}";
    });

    /// <summary>Adds the word of a spelling suggestion to the personal word list and closes the suggestion.</summary>
    /// <param name="id">The suggestion.</param>
    /// <returns>A task that completes when added.</returns>
    public Task AddWordAsync(Guid id) => DecideAsync(async () =>
    {
        if (Board.Find(id) is { } s && Document.AssistLanguage is { } language)
        {
            await api.AddLyricWordAsync(new LyricWordInput { Language = language, Word = s.Anchor.Original });
            Board.Merge((await api.RejectLyricSuggestionsAsync(Document.Id, Decision([id]))).Suggestions);
            Live = $"Added to my words: {s.Anchor.Original}";
        }
    });

    /// <summary>Undoes the newest accept.</summary>
    /// <returns>A task that completes when undone.</returns>
    public Task UndoAsync() => DecideAsync(async () =>
    {
        Document.Apply(await api.UndoLyricSuggestionsAsync(Document.Id, Document.Revision));
        Live = "Undone: the text before the last accept is back";
    });

    /// <summary>Acknowledges the Claude notice and runs the waiting request.</summary>
    /// <returns>A task that completes when the request was started.</returns>
    public async Task AcknowledgeNoticeAsync()
    {
        var pending = PendingNotice;
        PendingNotice = null;
        var (_, error) = await ApiCall.TryAsync(() => api.UpdateLyricAssistSettingsAsync(new LyricAssistSettingsUpdate { AcknowledgeClaudeNotice = true }));
        Error = error;
        if (error is null && pending is not null)
        {
            Status = Status is null ? null : Status with { ClaudeNoticeRequired = false };
            await StartAsync(pending);
        }
    }

    /// <summary>Drops the waiting request: nothing is sent.</summary>
    public void CancelNotice() => PendingNotice = null;

    /// <summary>Refreshes the suggestion list of the current revision (after decisions; re-anchored by the server).</summary>
    /// <returns>A task that completes when refreshed.</returns>
    public async Task RefreshSuggestionsAsync()
    {
        var (list, _) = await ApiCall.TryAsync(() => api.ListLyricSuggestionsAsync(Document.Id));
        if (list is not null)
        {
            Board.Merge(list.Suggestions);
        }
    }

    private static string AcceptedText(AcceptResultDto result)
    {
        var applied = result.Applied.Count == 1 ? "Suggestion accepted" : $"{AssistText.Number(result.Applied.Count)} suggestions accepted";
        return result.Stale.Count == 0 ? applied : $"{applied}; {AssistText.Number(result.Stale.Count)} outdated, not applied";
    }

    private SuggestionDecisionRequest Decision(IReadOnlyList<Guid> ids) => new() { SuggestionIds = ids, ExpectedRevision = Document.Revision };

    private async Task DecideAsync(Func<Task> action)
    {
        try
        {
            (Error, Refusal) = (null, null);
            await action();
            await RefreshSuggestionsAsync();
        }
        catch (ApiException ex)
        {
            await HandleAsync(ex.Error);
        }
    }

    private async Task HandleAsync(ApiError error)
    {
        if (error.Code == LyricAssistProblemTypes.RevisionConflict)
        {
            await ReloadAsync();
            Error = "The lyrics were changed elsewhere. They are reloaded; check the suggestions again.";
            return;
        }

        var refusal = AssistRefusal.From(error);
        var known = refusal.Code is { } code && IsRefusal(code);
        Refusal = known ? refusal : null;
        Error = known ? null : error.UserMessage;
    }

    private static bool IsRefusal(string code) => code is LyricAssistProblemTypes.NotOwnLyrics or LyricAssistProblemTypes.Imitation
        or LyricAssistProblemTypes.ModelUnavailable or LyricAssistProblemTypes.AssistActive or LyricAssistProblemTypes.LanguageUnsupported;

    private async Task ReloadAsync()
    {
        var (sheet, _) = await ApiCall.TryAsync(() => api.GetLyricsSheetAsync(Document.Id));
        if (sheet is not null)
        {
            Document.Load(sheet);
        }

        await RefreshSuggestionsAsync();
    }
}
