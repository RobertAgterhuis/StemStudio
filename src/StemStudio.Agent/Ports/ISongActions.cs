using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Agent.Ports;

/// <summary>
/// Own lyrics sheets and songs of the public API (feature 010) over loopback. Lyrics sheets come back as
/// <see cref="LyricsSheetSummary"/>: the implementation drops the text, so saved lyrics never pass through the model.
/// </summary>
public interface ISongActions
{
    /// <summary>GET /api/lyrics (list_lyrics_sheets), reduced to summaries.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Sheets, newest change first.</returns>
    Task<IReadOnlyList<LyricsSheetSummary>> ListLyricsSheetsAsync(CancellationToken cancellationToken);

    /// <summary>POST /api/lyrics (save_lyrics_sheet). The text was checked by the policy: it occurs in a user message.</summary>
    /// <param name="input">Title, language and the user's text.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The saved sheet without its text.</returns>
    Task<LyricsSheetSummary> CreateLyricsSheetAsync(LyricsInput input, CancellationToken cancellationToken);

    /// <summary>DELETE /api/lyrics/{lyricsId} (compensation of save_lyrics_sheet only; not a model tool).</summary>
    /// <param name="lyricsId">Sheet identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteLyricsSheetAsync(Guid lyricsId, CancellationToken cancellationToken);

    /// <summary>POST /api/songs (generate_song, heavy GPU job).</summary>
    /// <param name="request">Lyrics (sheet id or the user's text), style or guarded caption, voice and options.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued generation (kind song).</returns>
    Task<GenerationDto> CreateSongAsync(SongRequest request, CancellationToken cancellationToken);
}
