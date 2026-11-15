using System.Net.Http.Headers;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Web.Api;

/// <summary>Lyrics sheet and song operations (feature 010, backend contract 1.9.0).</summary>
public sealed partial class StemStudioApiClient
{
    /// <inheritdoc />
    public Task<IReadOnlyList<LyricsSheetDto>> ListLyricsSheetsAsync(string? query = null, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<LyricsSheetDto>>(HttpMethod.Get, query is null ? ApiRoutes.LyricsSheets : $"{ApiRoutes.LyricsSheets}?q={Uri.EscapeDataString(query)}", null, cancellationToken);

    /// <inheritdoc />
    public Task<LyricsSheetDto> GetLyricsSheetAsync(Guid lyricsId, CancellationToken cancellationToken = default) =>
        SendAsync<LyricsSheetDto>(HttpMethod.Get, ApiRoutes.LyricsSheet(lyricsId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LyricsSheetDto> CreateLyricsSheetAsync(LyricsInput input, CancellationToken cancellationToken = default) =>
        SendAsync<LyricsSheetDto>(HttpMethod.Post, ApiRoutes.LyricsSheets, JsonBody(input), cancellationToken);

    /// <inheritdoc />
    public Task<LyricsSheetDto> UploadLyricsSheetAsync(UploadFile file, string? title, string? language, CancellationToken cancellationToken = default)
    {
        var body = MultipartBody(file);
        AddField(body, LyricsUploadForm.Title, title);
        AddField(body, LyricsUploadForm.Language, language);
        return SendAsync<LyricsSheetDto>(HttpMethod.Post, ApiRoutes.LyricsSheetUpload, body, cancellationToken);
    }

    /// <inheritdoc />
    public Task<LyricsSheetDto> UpdateLyricsSheetAsync(Guid lyricsId, LyricsInput input, CancellationToken cancellationToken = default) =>
        SendAsync<LyricsSheetDto>(HttpMethod.Put, ApiRoutes.LyricsSheet(lyricsId), JsonBody(input), cancellationToken);

    /// <inheritdoc />
    public async Task DeleteLyricsSheetAsync(Guid lyricsId, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(HttpMethod.Delete, ApiRoutes.LyricsSheet(lyricsId), null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<SectionProposalDto> ProposeSectionsAsync(string text, CancellationToken cancellationToken = default) =>
        SendAsync<SectionProposalDto>(HttpMethod.Post, ApiRoutes.LyricsSections, JsonBody(new SectionsRequest { Text = text }), cancellationToken);

    /// <inheritdoc />
    public Task<GenerationDto> CreateSongAsync(SongRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GenerationDto>(HttpMethod.Post, ApiRoutes.Songs, JsonBody(request), cancellationToken);

    private static void AddField(MultipartFormDataContent body, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var content = new StringContent(value);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") { CharSet = "utf-8" };
            body.Add(content, name);
        }
    }
}
