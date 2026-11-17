using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.Ports;

/// <summary>
/// Lyric-assistant endpoints of the public API (feature 023, backend-api 1.23.0) over loopback, for the eight tools of catalogue
/// 1.2.0. No member returns sheet or suggestion text (FR-019): the implementation (<c>LoopbackLyricAssistActions</c>, backend T056)
/// projects the responses to the summaries below.
/// </summary>
public interface ILyricAssistActions
{
    /// <summary>GET /api/lyrics/{lyricsId}, reduced to revision, language and the untrusted title.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The sheet state.</returns>
    Task<LyricSheetState> GetSheetStateAsync(Guid lyricsId, CancellationToken cancellationToken);

    /// <summary>POST /api/lyrics/{lyricsId}/assists (check_lyrics, suggest_lyric_lines).</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="request">The request (expected revision from <see cref="GetSheetStateAsync"/>).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The new assist without suggestions.</returns>
    Task<LyricAssistSummaryDto> StartAssistAsync(Guid lyricsId, CreateLyricAssistRequest request, CancellationToken cancellationToken);

    /// <summary>GET /api/lyric-assists/{assistId} without the suggestions (wait_for_job phase <c>lyricAssist</c>).</summary>
    /// <param name="assistId">Assist id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The assist state.</returns>
    Task<LyricAssistSummaryDto> GetAssistAsync(Guid assistId, CancellationToken cancellationToken);

    /// <summary>GET /api/lyrics/{lyricsId}/suggestions, reduced to ids, lines, categories, types, severities, statuses and sources.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="status">Status filter.</param>
    /// <param name="category">Category filter.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The text-free list.</returns>
    Task<LyricSuggestionsSummary> ListSuggestionsAsync(Guid lyricsId, SuggestionStatusDto? status, SuggestionCategoryDto? category, CancellationToken cancellationToken);

    /// <summary>POST /api/lyrics/{lyricsId}/suggestions/accept (apply_lyric_suggestions, after approval).</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="request">Ids and expected revision.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>New revision and counts.</returns>
    Task<LyricAcceptSummary> AcceptAsync(Guid lyricsId, SuggestionDecisionRequest request, CancellationToken cancellationToken);

    /// <summary>POST /api/lyrics/{lyricsId}/undo (inverse of apply_lyric_suggestions).</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="expectedRevision">The revision the accept produced.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>New revision and counts.</returns>
    Task<LyricAcceptSummary> UndoAsync(Guid lyricsId, int expectedRevision, CancellationToken cancellationToken);

    /// <summary>POST /api/lyrics/{lyricsId}/suggestions/{suggestionId}/intentional (mark_lyrics_intentional).</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="suggestionId">Suggestion id.</param>
    /// <param name="request">Scope and expected revision.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The mark id and scope.</returns>
    Task<IntentionalMarkRef> MarkIntentionalAsync(Guid lyricsId, Guid suggestionId, MarkSuggestionIntentionalRequest request, CancellationToken cancellationToken);

    /// <summary>DELETE /api/lyrics/{lyricsId}/intentional-marks/{markId} (inverse of mark_lyrics_intentional).</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="markId">Mark id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteIntentionalMarkAsync(Guid lyricsId, Guid markId, CancellationToken cancellationToken);

    /// <summary>GET /api/lyric-assist/rhymes (find_rhymes; dictionary words, not lyrics).</summary>
    /// <param name="word">The word (from a user message).</param>
    /// <param name="language">Language.</param>
    /// <param name="limit">Maximum rhymes.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The rhymes.</returns>
    Task<RhymeResultDto> FindRhymesAsync(string word, AssistLanguageDto language, int? limit, CancellationToken cancellationToken);

    /// <summary>GET /api/lyrics/{lyricsId}/syllables (count_syllables; numbers and 010 section names only).</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The report.</returns>
    Task<SyllableReportDto> CountSyllablesAsync(Guid lyricsId, CancellationToken cancellationToken);
}
