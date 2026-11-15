using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Services;

namespace StemStudio.Web.Api;

/// <summary>Style library and generation operations (feature 009, backend contract 1.8.0).</summary>
public sealed partial class StemStudioApiClient
{
    /// <inheritdoc />
    public Task<GenerationStatusDto> GetGenerationStatusAsync(CancellationToken cancellationToken = default) =>
        SendAsync<GenerationStatusDto>(HttpMethod.Get, ApiRoutes.GenerationStatus, null, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<StyleDto>> ListStylesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<StyleDto>>(HttpMethod.Get, ApiRoutes.Styles, null, cancellationToken);

    /// <inheritdoc />
    public Task<StyleDto> GetStyleAsync(Guid styleId, CancellationToken cancellationToken = default) =>
        SendAsync<StyleDto>(HttpMethod.Get, ApiRoutes.Style(styleId), null, cancellationToken);

    /// <inheritdoc />
    public Task<StyleDto> CreateStyleAsync(StyleInput input, CancellationToken cancellationToken = default) =>
        SendAsync<StyleDto>(HttpMethod.Post, ApiRoutes.Styles, JsonBody(input), cancellationToken);

    /// <inheritdoc />
    public Task<StyleDto> UpdateStyleAsync(Guid styleId, StyleInput input, CancellationToken cancellationToken = default) =>
        SendAsync<StyleDto>(HttpMethod.Put, ApiRoutes.Style(styleId), JsonBody(input), cancellationToken);

    /// <inheritdoc />
    public Task<StyleDto> DuplicateStyleAsync(Guid styleId, CancellationToken cancellationToken = default) =>
        SendAsync<StyleDto>(HttpMethod.Post, ApiRoutes.DuplicateStyle(styleId), null, cancellationToken);

    /// <inheritdoc />
    public async Task DeleteStyleAsync(Guid styleId, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(HttpMethod.Delete, ApiRoutes.Style(styleId), null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<GenerationDto> CreateRestyleAsync(Guid trackId, RestyleRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GenerationDto>(HttpMethod.Post, ApiRoutes.Restyles(trackId), JsonBody(request), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<GenerationDto>> ListGenerationsAsync(GenerationQuery query, CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<GenerationDto>>(HttpMethod.Get, GenerationsQuery(query), null, cancellationToken);

    /// <inheritdoc />
    public Task<GenerationDto> GetGenerationAsync(Guid generationId, CancellationToken cancellationToken = default) =>
        SendAsync<GenerationDto>(HttpMethod.Get, ApiRoutes.Generation(generationId), null, cancellationToken);

    /// <inheritdoc />
    public Task<GenerationDto> CancelGenerationAsync(Guid generationId, CancellationToken cancellationToken = default) =>
        SendAsync<GenerationDto>(HttpMethod.Post, ApiRoutes.CancelGeneration(generationId), null, cancellationToken);

    /// <inheritdoc />
    public Task<GenerationDto> RetryGenerationAsync(Guid generationId, CancellationToken cancellationToken = default) =>
        SendAsync<GenerationDto>(HttpMethod.Post, ApiRoutes.RetryGeneration(generationId), null, cancellationToken);

    /// <inheritdoc />
    public async Task DeleteGenerationAsync(Guid generationId, CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(HttpMethod.Delete, ApiRoutes.Generation(generationId), null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TrackDto> ImportTakeAsync(Guid generationId, int index, CancellationToken cancellationToken = default) =>
        SendAsync<TrackDto>(HttpMethod.Post, ApiRoutes.ImportTake(generationId, index), null, cancellationToken);

    private static string GenerationsQuery(GenerationQuery query)
    {
        var filters = new List<string>(2);
        if (query.Kind is { } kind)
        {
            filters.Add($"kind={WireName.Of(kind)}");
        }

        if (query.SourceTrackId is { } trackId)
        {
            filters.Add($"sourceTrackId={trackId}");
        }

        return filters.Count == 0 ? ApiRoutes.Generations : $"{ApiRoutes.Generations}?{string.Join('&', filters)}";
    }
}
