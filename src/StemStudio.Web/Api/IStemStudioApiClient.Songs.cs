using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Web.Api;

/// <summary>Own lyrics sheets and songs (feature 010, backend contract 1.9.0).</summary>
public partial interface IStemStudioApiClient
{
    /// <summary><c>GET /api/lyrics[?q=]</c>: saved lyrics sheets, most recently changed first.</summary>
    /// <param name="query">Optional search text.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The sheets.</returns>
    Task<IReadOnlyList<LyricsSheetDto>> ListLyricsSheetsAsync(string? query = null, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/lyrics/{id}</c>.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The sheet; 404 throws <see cref="ApiException"/>.</returns>
    Task<LyricsSheetDto> GetLyricsSheetAsync(Guid lyricsId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyrics</c>: saves a new sheet (the API normalises the text).</summary>
    /// <param name="input">Title, language and text.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The created sheet; 400 (field + reason) throws <see cref="ApiException"/>.</returns>
    Task<LyricsSheetDto> CreateLyricsSheetAsync(LyricsInput input, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyrics/upload</c>: saves a <c>.txt</c>, <c>.lrc</c> or <c>.md</c> file as a sheet (timestamps dropped).</summary>
    /// <param name="file">The file (UTF-8, at most 64 KiB).</param>
    /// <param name="title">Optional title (default the file name).</param>
    /// <param name="language">Optional language code.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The created sheet; 400, 413 and 415 throw <see cref="ApiException"/>.</returns>
    Task<LyricsSheetDto> UploadLyricsSheetAsync(UploadFile file, string? title, string? language, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/lyrics/{id}</c>: replaces a sheet; songs made earlier keep their copy.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="input">Title, language and text.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The updated sheet.</returns>
    Task<LyricsSheetDto> UpdateLyricsSheetAsync(Guid lyricsId, LyricsInput input, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/lyrics/{id}</c>.</summary>
    /// <param name="lyricsId">The sheet.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the sheet is deleted (204).</returns>
    Task DeleteLyricsSheetAsync(Guid lyricsId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/lyrics/sections</c>: proposes section markers for a text.</summary>
    /// <param name="text">The lyrics.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The text with markers, the sections and the density.</returns>
    Task<SectionProposalDto> ProposeSectionsAsync(string text, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/songs</c>: generates a song from own lyrics.</summary>
    /// <param name="request">The lyrics and the choices.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued generation (202); 400 (incl. <c>imitation</c>) and 409 throw <see cref="ApiException"/>.</returns>
    Task<GenerationDto> CreateSongAsync(SongRequest request, CancellationToken cancellationToken = default);
}
