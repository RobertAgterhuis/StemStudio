using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>Sheet settings (sensitivity, rhythm target), the own-work override (FR-031), the language guess (FR-001) and the
/// lists of the pane: marks, words, the assist log and the revisions.</summary>
public sealed partial class LyricAssistSession
{
    /// <summary>The intentional marks of the sheet (after <see cref="LoadMarksAsync"/>).</summary>
    public IReadOnlyList<IntentionalMarkDto> Marks { get; private set; } = [];

    /// <summary>The personal words of the sheet's language (after <see cref="LoadWordsAsync"/>).</summary>
    public IReadOnlyList<LyricWordDto> Words { get; private set; } = [];

    /// <summary>The newest assists of the sheet (Expert log).</summary>
    public IReadOnlyList<LyricAssistSummaryDto> History { get; private set; } = [];

    /// <summary>The kept revisions (Expert).</summary>
    public IReadOnlyList<LyricsRevisionSummaryDto> Revisions { get; private set; } = [];

    /// <summary>The language guess for an <c>unknown</c> sheet, or null.</summary>
    public LanguageGuessDto? Guess { get; private set; }

    /// <summary>Sets the sensitivity (the rhythm target is always sent, so it is kept).</summary>
    /// <param name="sensitivity">The sensitivity.</param>
    /// <returns>A task that completes when stored.</returns>
    public Task SetSensitivityAsync(SensitivityDto sensitivity) =>
        UpdateSettingsAsync(new SheetAssistSettingsUpdate { Sensitivity = sensitivity, RhythmTarget = Document.Assist.RhythmTarget });

    /// <summary>Saves or clears (null) the rhythm target.</summary>
    /// <param name="target">The target, or null.</param>
    /// <returns>A task that completes when stored.</returns>
    public Task SetRhythmTargetAsync(RhythmTargetDto? target) => UpdateSettingsAsync(new SheetAssistSettingsUpdate { RhythmTarget = target });

    /// <summary>The own-work statement and its version (<c>GET /api/lyric-assist/settings</c>).</summary>
    /// <returns>The settings, or null with <see cref="Error"/> set.</returns>
    public async Task<LyricAssistSettingsDto?> LoadSettingsAsync()
    {
        var (settings, error) = await ApiCall.TryAsync(() => api.GetLyricAssistSettingsAsync());
        Error = error;
        return settings;
    }

    /// <summary>Confirms the own-work statement for the matched track of the refusal.</summary>
    /// <param name="name">The typed name.</param>
    /// <param name="statementVersion">The version of the shown statement.</param>
    /// <returns>True when stored.</returns>
    public async Task<bool> ConfirmOwnWorkAsync(string name, string statementVersion)
    {
        if (Refusal is not { IsNotOwn: true, Overridable: true, MatchedTrackId: { } track })
        {
            return false;
        }

        var body = new OwnWorkConfirmRequest { MatchedTrackId = track, ConfirmedBy = name.Trim(), StatementVersion = statementVersion, Confirm = true };
        var (confirmed, error) = await ApiCall.TryAsync(() => api.ConfirmOwnWorkAsync(Document.Id, body));
        Error = error;
        if (confirmed is not null)
        {
            Document.SetAssist(Document.Assist with { OwnWorkOverride = confirmed });
            (Refusal, Live) = (null, "Own work confirmed");
        }

        return confirmed is not null;
    }

    /// <summary>Revokes the own-work override.</summary>
    /// <returns>A task that completes when revoked.</returns>
    public async Task RevokeOwnWorkAsync()
    {
        Error = await ApiCall.RunAsync(() => api.RevokeOwnWorkAsync(Document.Id));
        if (Error is null)
        {
            Document.SetAssist(Document.Assist with { OwnWorkOverride = null });
            Live = "Own work confirmation revoked";
        }
    }

    /// <summary>Guesses the language of an <c>unknown</c> sheet.</summary>
    /// <returns>A task that completes when guessed.</returns>
    public async Task GuessLanguageAsync()
    {
        if (Document.Sheet.Language == "unknown" && Document.Text.Trim().Length > 0)
        {
            (Guess, _) = await ApiCall.TryAsync(() => api.GuessLyricsLanguageAsync(Document.Text));
        }
    }

    /// <summary>Sets the guessed language and saves the sheet.</summary>
    /// <returns>A task that completes when saved.</returns>
    public async Task ApplyGuessAsync()
    {
        if (Guess?.Language is { } language)
        {
            Document.Language = AssistText.Wire(language);
            if (await SaveIfDirtyAsync())
            {
                Guess = null;
            }
        }
    }

    /// <summary>Loads the intentional marks.</summary>
    /// <returns>A task that completes when loaded.</returns>
    public async Task LoadMarksAsync() => (Marks, Error) = await Listed(() => api.ListIntentionalMarksAsync(Document.Id));

    /// <summary>Marks the selected text as intentional (word, phrase or the whole line).</summary>
    /// <param name="text">The selected text.</param>
    /// <param name="wholeLine">True when the selection is a whole line.</param>
    /// <returns>A task that completes when marked.</returns>
    public async Task MarkSelectionAsync(string text, bool wholeLine)
    {
        var trimmed = text.Trim();
        var scope = wholeLine ? IntentionalScopeDto.Line : trimmed.Contains(' ', StringComparison.Ordinal) ? IntentionalScopeDto.Phrase : IntentionalScopeDto.Word;
        if (trimmed.Length > 0 && await SaveIfDirtyAsync())
        {
            Error = await ApiCall.RunAsync(() => api.CreateIntentionalMarkAsync(Document.Id, new IntentionalMarkInput { Scope = scope, Text = trimmed }));
            await LoadMarksAsync();
        }
    }

    /// <summary>Deletes a mark.</summary>
    /// <param name="markId">The mark.</param>
    /// <returns>A task that completes when deleted.</returns>
    public async Task DeleteMarkAsync(Guid markId)
    {
        Error = await ApiCall.RunAsync(() => api.DeleteIntentionalMarkAsync(Document.Id, markId));
        await LoadMarksAsync();
    }

    /// <summary>Loads the personal words of the sheet's language.</summary>
    /// <returns>A task that completes when loaded.</returns>
    public async Task LoadWordsAsync()
    {
        if (Document.AssistLanguage is { } language)
        {
            (Words, Error) = await Listed(() => api.ListLyricWordsAsync(language));
        }
    }

    /// <summary>Deletes a personal word.</summary>
    /// <param name="wordId">The word.</param>
    /// <returns>A task that completes when deleted.</returns>
    public async Task DeleteWordAsync(Guid wordId)
    {
        Error = await ApiCall.RunAsync(() => api.DeleteLyricWordAsync(wordId));
        await LoadWordsAsync();
    }

    /// <summary>Loads the assist log (Expert).</summary>
    /// <returns>A task that completes when loaded.</returns>
    public async Task LoadHistoryAsync() => (History, Error) = await Listed(() => api.ListLyricAssistsAsync(Document.Id));

    /// <summary>Loads the revision list (Expert).</summary>
    /// <returns>A task that completes when loaded.</returns>
    public async Task LoadRevisionsAsync() => (Revisions, Error) = await Listed(() => api.ListLyricsRevisionsAsync(Document.Id));

    /// <summary>Restores a revision as a new revision.</summary>
    /// <param name="revision">The revision.</param>
    /// <returns>A task that completes when restored.</returns>
    public async Task RestoreAsync(int revision)
    {
        await DecideAsync(async () =>
        {
            Document.Apply(await api.RestoreLyricsRevisionAsync(Document.Id, revision, Document.Revision));
            Live = $"Revision {AssistText.Number(revision)} restored";
        });
        await LoadRevisionsAsync();
    }

    private static async Task<(IReadOnlyList<T> Items, string? Error)> Listed<T>(Func<Task<IReadOnlyList<T>>> call)
    {
        var (items, error) = await ApiCall.TryAsync(call);
        return (items ?? [], error);
    }

    private async Task UpdateSettingsAsync(SheetAssistSettingsUpdate update)
    {
        var (assist, error) = await ApiCall.TryAsync(() => api.UpdateSheetAssistSettingsAsync(Document.Id, update));
        Error = error;
        if (assist is not null)
        {
            Document.SetAssist(assist);
        }
    }
}
