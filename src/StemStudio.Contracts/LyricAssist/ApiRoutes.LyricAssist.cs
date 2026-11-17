using System.Globalization;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Contracts;

/// <summary>Routes of the lyric-assistant endpoints (feature 023, backend-api 1.23.0).</summary>
public static partial class ApiRoutes
{
    /// <summary>Root of the assistant resources that are not scoped to one sheet.</summary>
    public const string LyricAssist = "/api/lyric-assist";

    /// <summary>GET: availability of the layers and the Claude notice state.</summary>
    public const string LyricAssistStatus = LyricAssist + "/status";

    /// <summary>PUT: acknowledge or withdraw the lyric-assistant Claude notice.</summary>
    public const string LyricAssistSettings = LyricAssist + "/settings";

    /// <summary>POST: syllables of unsaved editor text.</summary>
    public const string LyricAssistSyllables = LyricAssist + "/syllables";

    /// <summary>GET: rhymes from local data (query <c>word</c>, <c>language</c>, <c>limit</c>).</summary>
    public const string LyricAssistRhymes = LyricAssist + "/rhymes";

    /// <summary>GET (query <c>language</c>) / POST: the personal word list.</summary>
    public const string LyricAssistWords = LyricAssist + "/words";

    /// <summary>POST: language guess for an <c>unknown</c> sheet.</summary>
    public const string LyricAssistLanguageGuess = LyricAssist + "/language-guess";

    /// <summary>Root of single assists.</summary>
    public const string LyricAssists = "/api/lyric-assists";

    /// <summary>DELETE: one personal word.</summary>
    /// <param name="wordId">Word identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricAssistWord(Guid wordId) => $"{LyricAssistWords}/{wordId}";

    /// <summary>GET rhymes for a word.</summary>
    /// <param name="word">The word.</param>
    /// <param name="language">Language.</param>
    /// <param name="limit">Maximum rhymes; null = the default.</param>
    /// <returns>The route with its query.</returns>
    public static string LyricAssistRhymesFor(string word, AssistLanguageDto language, int? limit = null) =>
        $"{LyricAssistRhymes}?word={Uri.EscapeDataString(word)}&language={Wire(language)}" + (limit is null ? string.Empty : $"&limit={Number(limit.Value)}");

    /// <summary>GET the personal words of one language.</summary>
    /// <param name="language">Language.</param>
    /// <returns>The route with its query.</returns>
    public static string LyricAssistWordsFor(AssistLanguageDto language) => $"{LyricAssistWords}?language={Wire(language)}";

    /// <summary>GET: snapshot of an assist with its suggestions.</summary>
    /// <param name="assistId">Assist identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricAssistById(Guid assistId) => $"{LyricAssists}/{assistId}";

    /// <summary>GET: SSE events of an assist, optionally only after a sequence number.</summary>
    /// <param name="assistId">Assist identifier.</param>
    /// <param name="after">Last sequence number already seen.</param>
    /// <returns>The route.</returns>
    public static string LyricAssistEvents(Guid assistId, int? after = null) =>
        after is null ? $"{LyricAssistById(assistId)}/events" : $"{LyricAssistById(assistId)}/events?after={Number(after.Value)}";

    /// <summary>POST: cancel an assist.</summary>
    /// <param name="assistId">Assist identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricAssistCancel(Guid assistId) => $"{LyricAssistById(assistId)}/cancel";

    /// <summary>POST (start) / GET (newest 20) assists of a sheet.</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricsAssists(Guid lyricsId) => $"{LyricsSheet(lyricsId)}/assists";

    /// <summary>GET: suggestions of a sheet, optionally filtered.</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <param name="status">Status filter.</param>
    /// <param name="category">Category filter.</param>
    /// <returns>The route with its query.</returns>
    public static string LyricsSuggestions(Guid lyricsId, SuggestionStatusDto? status = null, SuggestionCategoryDto? category = null)
    {
        var query = new[] { status is null ? null : "status=" + Wire(status.Value), category is null ? null : "category=" + Wire(category.Value) }
            .Where(part => part is not null);
        var text = string.Join('&', query);
        return $"{LyricsSheet(lyricsId)}/suggestions" + (text.Length == 0 ? string.Empty : "?" + text);
    }

    /// <summary>POST: accept suggestions (one revision, one undo step).</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricsSuggestionsAccept(Guid lyricsId) => $"{LyricsSuggestions(lyricsId)}/accept";

    /// <summary>POST: reject suggestions.</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricsSuggestionsReject(Guid lyricsId) => $"{LyricsSuggestions(lyricsId)}/reject";

    /// <summary>POST: mark the text of a suggestion as intentional.</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <param name="suggestionId">Suggestion identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricsSuggestionIntentional(Guid lyricsId, Guid suggestionId) => $"{LyricsSuggestions(lyricsId)}/{suggestionId}/intentional";

    /// <summary>GET / POST intentional marks of a sheet.</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricsIntentionalMarks(Guid lyricsId) => $"{LyricsSheet(lyricsId)}/intentional-marks";

    /// <summary>DELETE one intentional mark.</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <param name="markId">Mark identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricsIntentionalMark(Guid lyricsId, Guid markId) => $"{LyricsIntentionalMarks(lyricsId)}/{markId}";

    /// <summary>PUT: sensitivity and rhythm target of a sheet.</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricsAssistSettings(Guid lyricsId) => $"{LyricsSheet(lyricsId)}/assist-settings";

    /// <summary>PUT (confirm) / DELETE (revoke) the own-work override of a sheet (not an agent tool).</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricsOwnWorkOverride(Guid lyricsId) => $"{LyricsSheet(lyricsId)}/own-work-override";

    /// <summary>POST: undo the newest accept.</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricsUndo(Guid lyricsId) => $"{LyricsSheet(lyricsId)}/undo";

    /// <summary>GET: revisions of a sheet (Expert).</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricsRevisions(Guid lyricsId) => $"{LyricsSheet(lyricsId)}/revisions";

    /// <summary>POST: restore a revision as a new revision.</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <param name="revision">Revision to restore.</param>
    /// <returns>The route.</returns>
    public static string LyricsRevisionRestore(Guid lyricsId, int revision) => $"{LyricsRevisions(lyricsId)}/{Number(revision)}/restore";

    /// <summary>GET: syllables of the saved sheet.</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <returns>The route.</returns>
    public static string LyricsSyllables(Guid lyricsId) => $"{LyricsSheet(lyricsId)}/syllables";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Wire<T>(T value)
        where T : struct, Enum =>
        System.Text.Json.JsonSerializer.Serialize(value).Trim('"');
}
