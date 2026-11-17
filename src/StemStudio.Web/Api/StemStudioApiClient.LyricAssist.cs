using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Agent;
using StemStudio.Web.Components.Lyrics.Assist;

namespace StemStudio.Web.Api;

/// <summary>Lyric assistant operations (feature 023, backend contract 1.23.0).</summary>
public sealed partial class StemStudioApiClient
{
    /// <inheritdoc />
    public Task<LyricAssistStatusDto> GetLyricAssistStatusAsync(CancellationToken cancellationToken = default) =>
        SendAsync<LyricAssistStatusDto>(HttpMethod.Get, ApiRoutes.LyricAssistStatus, null, cancellationToken);

    /// <inheritdoc />
    public Task<LyricAssistSettingsDto> GetLyricAssistSettingsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<LyricAssistSettingsDto>(HttpMethod.Get, ApiRoutes.LyricAssistSettings, null, cancellationToken);

    /// <inheritdoc />
    public Task<LyricAssistSettingsDto> UpdateLyricAssistSettingsAsync(LyricAssistSettingsUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<LyricAssistSettingsDto>(HttpMethod.Put, ApiRoutes.LyricAssistSettings, JsonBody(update), cancellationToken);

    /// <inheritdoc />
    public Task<LyricAssistDto> CreateLyricAssistAsync(Guid lyricsId, CreateLyricAssistRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<LyricAssistDto>(HttpMethod.Post, ApiRoutes.LyricsAssists(lyricsId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LyricAssistSummaryDto>> ListLyricAssistsAsync(Guid lyricsId, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<LyricAssistSummaryDto>>(HttpMethod.Get, ApiRoutes.LyricsAssists(lyricsId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LyricAssistDto> GetLyricAssistAsync(Guid assistId, CancellationToken cancellationToken = default) =>
        SendAsync<LyricAssistDto>(HttpMethod.Get, ApiRoutes.LyricAssistById(assistId), null, cancellationToken);

    /// <inheritdoc />
    public async IAsyncEnumerable<LyricAssistEvent> StreamLyricAssistEventsAsync(Guid assistId, int? after = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var response = await _transport.SendRawAsync(EventsRequest(assistId, after), cancellationToken, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await foreach (var message in SseReader.ReadAsync(stream, cancellationToken))
        {
            var e = ToAssistEvent(message);
            yield return e;
            if (e.IsTerminal)
            {
                yield break;
            }
        }
    }

    /// <inheritdoc />
    public Task<LyricAssistDto> CancelLyricAssistAsync(Guid assistId, CancellationToken cancellationToken = default) =>
        SendAsync<LyricAssistDto>(HttpMethod.Post, ApiRoutes.LyricAssistCancel(assistId), null, cancellationToken);

    /// <inheritdoc />
    public Task<SuggestionListDto> ListLyricSuggestionsAsync(Guid lyricsId, SuggestionStatusDto? status = null, CancellationToken cancellationToken = default) =>
        SendAsync<SuggestionListDto>(HttpMethod.Get, ApiRoutes.LyricsSuggestions(lyricsId, status), null, cancellationToken);

    /// <inheritdoc />
    public Task<AcceptResultDto> AcceptLyricSuggestionsAsync(Guid lyricsId, SuggestionDecisionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AcceptResultDto>(HttpMethod.Post, ApiRoutes.LyricsSuggestionsAccept(lyricsId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<SuggestionListDto> RejectLyricSuggestionsAsync(Guid lyricsId, SuggestionDecisionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SuggestionListDto>(HttpMethod.Post, ApiRoutes.LyricsSuggestionsReject(lyricsId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<IntentionalMarkDto> MarkSuggestionIntentionalAsync(Guid lyricsId, Guid suggestionId, MarkSuggestionIntentionalRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<IntentionalMarkDto>(HttpMethod.Post, ApiRoutes.LyricsSuggestionIntentional(lyricsId, suggestionId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IntentionalMarkDto>> ListIntentionalMarksAsync(Guid lyricsId, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<IntentionalMarkDto>>(HttpMethod.Get, ApiRoutes.LyricsIntentionalMarks(lyricsId), null, cancellationToken);

    /// <inheritdoc />
    public Task<IntentionalMarkDto> CreateIntentionalMarkAsync(Guid lyricsId, IntentionalMarkInput input, CancellationToken cancellationToken = default) =>
        SendAsync<IntentionalMarkDto>(HttpMethod.Post, ApiRoutes.LyricsIntentionalMarks(lyricsId), JsonBody(input), cancellationToken);

    /// <inheritdoc />
    public Task DeleteIntentionalMarkAsync(Guid lyricsId, Guid markId, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(HttpMethod.Delete, ApiRoutes.LyricsIntentionalMark(lyricsId, markId), null, cancellationToken);

    /// <inheritdoc />
    public Task<SheetAssistInfoDto> UpdateSheetAssistSettingsAsync(Guid lyricsId, SheetAssistSettingsUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<SheetAssistInfoDto>(HttpMethod.Put, ApiRoutes.LyricsAssistSettings(lyricsId), JsonBody(update), cancellationToken);

    /// <inheritdoc />
    public Task<OwnWorkOverrideDto> ConfirmOwnWorkAsync(Guid lyricsId, OwnWorkConfirmRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OwnWorkOverrideDto>(HttpMethod.Put, ApiRoutes.LyricsOwnWorkOverride(lyricsId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task RevokeOwnWorkAsync(Guid lyricsId, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(HttpMethod.Delete, ApiRoutes.LyricsOwnWorkOverride(lyricsId), null, cancellationToken);

    /// <inheritdoc />
    public Task<AcceptResultDto> UndoLyricSuggestionsAsync(Guid lyricsId, int expectedRevision, CancellationToken cancellationToken = default) =>
        SendAsync<AcceptResultDto>(HttpMethod.Post, ApiRoutes.LyricsUndo(lyricsId), JsonBody(new RevisionRequest { ExpectedRevision = expectedRevision }), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LyricsRevisionSummaryDto>> ListLyricsRevisionsAsync(Guid lyricsId, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<LyricsRevisionSummaryDto>>(HttpMethod.Get, ApiRoutes.LyricsRevisions(lyricsId), null, cancellationToken);

    /// <inheritdoc />
    public Task<AcceptResultDto> RestoreLyricsRevisionAsync(Guid lyricsId, int revision, int expectedRevision, CancellationToken cancellationToken = default) =>
        SendAsync<AcceptResultDto>(HttpMethod.Post, ApiRoutes.LyricsRevisionRestore(lyricsId, revision), JsonBody(new RevisionRequest { ExpectedRevision = expectedRevision }), cancellationToken);

    /// <inheritdoc />
    public Task<SyllableReportDto> CountSyllablesAsync(SyllableRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SyllableReportDto>(HttpMethod.Post, ApiRoutes.LyricAssistSyllables, JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<RhymeResultDto> FindRhymesAsync(string word, AssistLanguageDto language, CancellationToken cancellationToken = default) =>
        SendAsync<RhymeResultDto>(HttpMethod.Get, ApiRoutes.LyricAssistRhymesFor(word, language), null, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LyricWordDto>> ListLyricWordsAsync(AssistLanguageDto language, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<LyricWordDto>>(HttpMethod.Get, ApiRoutes.LyricAssistWordsFor(language), null, cancellationToken);

    /// <inheritdoc />
    public Task<LyricWordDto> AddLyricWordAsync(LyricWordInput input, CancellationToken cancellationToken = default) =>
        SendAsync<LyricWordDto>(HttpMethod.Post, ApiRoutes.LyricAssistWords, JsonBody(input), cancellationToken);

    /// <inheritdoc />
    public Task DeleteLyricWordAsync(Guid wordId, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(HttpMethod.Delete, ApiRoutes.LyricAssistWord(wordId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LanguageGuessDto> GuessLyricsLanguageAsync(string text, CancellationToken cancellationToken = default) =>
        SendAsync<LanguageGuessDto>(HttpMethod.Post, ApiRoutes.LyricAssistLanguageGuess, JsonBody(new LanguageGuessRequest { Text = text }), cancellationToken);

    private static HttpRequestMessage EventsRequest(Guid assistId, int? after)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ApiRoutes.LyricAssistEvents(assistId, after), UriKind.Relative));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    private static LyricAssistEvent ToAssistEvent(SseMessage message)
    {
        var id = int.TryParse(message.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var seq) ? seq : 0;
        using var document = JsonDocument.Parse(message.Data);
        return new LyricAssistEvent(id, message.Event, document.RootElement.Clone());
    }
}
