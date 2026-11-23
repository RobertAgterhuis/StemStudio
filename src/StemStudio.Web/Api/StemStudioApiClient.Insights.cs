using System.Net;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Contracts.Insights;

namespace StemStudio.Web.Api;

/// <summary>Stem quality report, lesson handouts and practice notes (feature 020, backend contract 1.19.0).</summary>
public sealed partial class StemStudioApiClient
{
    /// <inheritdoc />
    public Task<QualityReportDto> GetQualityReportAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<QualityReportDto>(HttpMethod.Get, ApiRoutes.Quality(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<QualityReportDto> RequestQualityReportAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<QualityReportDto>(HttpMethod.Post, ApiRoutes.Quality(trackId), null, cancellationToken);

    /// <inheritdoc />
    public Task<HandoutOptionsDto> GetHandoutOptionsAsync(
        HandoutSourceDto source, HandoutFamilyDto family, TranspositionDto? transposition = null, CancellationToken cancellationToken = default) =>
        SendAsync<HandoutOptionsDto>(HttpMethod.Get, ApiRoutes.HandoutOptions(source, family, transposition), null, cancellationToken);

    /// <inheritdoc />
    public Task<AssetDto> CreateHandoutAsync(CreateHandoutRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AssetDto>(HttpMethod.Post, ApiRoutes.Handouts, JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<PracticeNotesDto> GetPracticeNotesAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        SendAsync<PracticeNotesDto>(HttpMethod.Get, ApiRoutes.PracticeNotes(trackId), null, cancellationToken);

    /// <inheritdoc />
    public async Task<PracticeNoteDto?> SetPracticeNoteAsync(Guid trackId, HandoutFamilyDto family, string text, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(
            HttpMethod.Put, ApiRoutes.PracticeNote(trackId, family), JsonBody(new SetPracticeNoteRequest { Text = text }), cancellationToken);
        return response.StatusCode == HttpStatusCode.NoContent
            ? null
            : await response.Content.ReadFromJsonAsync<PracticeNoteDto>(Json, cancellationToken);
    }
}
