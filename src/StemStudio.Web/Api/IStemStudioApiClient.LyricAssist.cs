using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Components.Lyrics.Assist;

namespace StemStudio.Web.Api;

/// <summary>The lyric assistant (feature 023, backend contract 1.23.0). Problems throw <see cref="ApiException"/>.</summary>
public partial interface IStemStudioApiClient
{
    /// <summary><c>GET /api/lyric-assist/status</c>: layers, model and the Claude notice state.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The status.</returns>
    Task<LyricAssistStatusDto> GetLyricAssistStatusAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/lyric-assist/settings</c>: the Claude notice and the own-work statement with their versions.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The settings.</returns>
    Task<LyricAssistSettingsDto> GetLyricAssistSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/lyric-assist/settings</c>: acknowledges (or withdraws) the Claude notice.</summary>
    /// <param name="update">The acknowledgement.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The stored settings.</returns>
    Task<LyricAssistSettingsDto> UpdateLyricAssistSettingsAsync(LyricAssistSettingsUpdate update, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyrics/{id}/assists</c>: starts an assist (202).</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="request">Kind, revision, range, count and instruction.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued assist with the suggestions known so far.</returns>
    Task<LyricAssistDto> CreateLyricAssistAsync(Guid lyricsId, CreateLyricAssistRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/lyrics/{id}/assists</c>: the newest 20 assists of a sheet.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The summaries, newest first.</returns>
    Task<IReadOnlyList<LyricAssistSummaryDto>> ListLyricAssistsAsync(Guid lyricsId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/lyric-assists/{id}</c>: snapshot of an assist.</summary>
    /// <param name="assistId">The assist.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The assist with its suggestions.</returns>
    Task<LyricAssistDto> GetLyricAssistAsync(Guid assistId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/lyric-assists/{id}/events</c>: the events of an assist until the terminal one (server-sent events).</summary>
    /// <param name="assistId">The assist.</param>
    /// <param name="after">The last sequence number already seen, or null for all.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>The events in order; the sequence ends when the stream closes.</returns>
    IAsyncEnumerable<LyricAssistEvent> StreamLyricAssistEventsAsync(Guid assistId, int? after = null, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyric-assists/{id}/cancel</c>.</summary>
    /// <param name="assistId">The assist.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The assist.</returns>
    Task<LyricAssistDto> CancelLyricAssistAsync(Guid assistId, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/lyrics/{id}/suggestions</c>.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="status">Status filter, or null.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The suggestions of the current revision.</returns>
    Task<SuggestionListDto> ListLyricSuggestionsAsync(Guid lyricsId, SuggestionStatusDto? status = null, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyrics/{id}/suggestions/accept</c>: one revision, one undo step.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="request">Ids and the expected revision.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new text and the applied and stale ids.</returns>
    Task<AcceptResultDto> AcceptLyricSuggestionsAsync(Guid lyricsId, SuggestionDecisionRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyrics/{id}/suggestions/reject</c>.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="request">Ids and the expected revision.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The suggestions after the decision.</returns>
    Task<SuggestionListDto> RejectLyricSuggestionsAsync(Guid lyricsId, SuggestionDecisionRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyrics/{id}/suggestions/{suggestionId}/intentional</c>.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="suggestionId">The suggestion.</param>
    /// <param name="request">Scope and expected revision.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The created mark.</returns>
    Task<IntentionalMarkDto> MarkSuggestionIntentionalAsync(Guid lyricsId, Guid suggestionId, MarkSuggestionIntentionalRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/lyrics/{id}/intentional-marks</c>.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The marks.</returns>
    Task<IReadOnlyList<IntentionalMarkDto>> ListIntentionalMarksAsync(Guid lyricsId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyrics/{id}/intentional-marks</c>: marks a selection.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="input">Scope and text.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The created mark.</returns>
    Task<IntentionalMarkDto> CreateIntentionalMarkAsync(Guid lyricsId, IntentionalMarkInput input, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/lyrics/{id}/intentional-marks/{markId}</c>.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="markId">The mark.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when deleted.</returns>
    Task DeleteIntentionalMarkAsync(Guid lyricsId, Guid markId, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/lyrics/{id}/assist-settings</c>: sensitivity and rhythm target (the target is always sent).</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="update">The new values.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The sheet's assistant info.</returns>
    Task<SheetAssistInfoDto> UpdateSheetAssistSettingsAsync(Guid lyricsId, SheetAssistSettingsUpdate update, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/lyrics/{id}/own-work-override</c>: confirms that the text is the user's own work.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="request">Track, name, statement version and the tick box.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The override.</returns>
    Task<OwnWorkOverrideDto> ConfirmOwnWorkAsync(Guid lyricsId, OwnWorkConfirmRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/lyrics/{id}/own-work-override</c>.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when revoked.</returns>
    Task RevokeOwnWorkAsync(Guid lyricsId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyrics/{id}/undo</c>: undoes the newest accept.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="expectedRevision">The revision the editor shows.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The restored text.</returns>
    Task<AcceptResultDto> UndoLyricSuggestionsAsync(Guid lyricsId, int expectedRevision, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/lyrics/{id}/revisions</c>.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The kept revisions, newest first.</returns>
    Task<IReadOnlyList<LyricsRevisionSummaryDto>> ListLyricsRevisionsAsync(Guid lyricsId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyrics/{id}/revisions/{revision}/restore</c>: restores a revision as a new one.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="revision">The revision to restore.</param>
    /// <param name="expectedRevision">The revision the editor shows.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The restored text.</returns>
    Task<AcceptResultDto> RestoreLyricsRevisionAsync(Guid lyricsId, int revision, int expectedRevision, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyric-assist/syllables</c>: syllables of (unsaved) editor text; sends nothing outside the machine.</summary>
    /// <param name="request">Language, text and rhythm target.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report.</returns>
    Task<SyllableReportDto> CountSyllablesAsync(SyllableRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/lyric-assist/rhymes</c>: rhymes from local data.</summary>
    /// <param name="word">The word.</param>
    /// <param name="language">The language.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The rhymes.</returns>
    Task<RhymeResultDto> FindRhymesAsync(string word, AssistLanguageDto language, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/lyric-assist/words?language=</c>: the personal word list.</summary>
    /// <param name="language">The language.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The words.</returns>
    Task<IReadOnlyList<LyricWordDto>> ListLyricWordsAsync(AssistLanguageDto language, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyric-assist/words</c>.</summary>
    /// <param name="input">Language and word.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The stored word.</returns>
    Task<LyricWordDto> AddLyricWordAsync(LyricWordInput input, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/lyric-assist/words/{wordId}</c>.</summary>
    /// <param name="wordId">The word.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when deleted.</returns>
    Task DeleteLyricWordAsync(Guid wordId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyric-assist/language-guess</c>: deterministic guess for a sheet in language <c>unknown</c>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The guess.</returns>
    Task<LanguageGuessDto> GuessLyricsLanguageAsync(string text, CancellationToken cancellationToken = default);
}
