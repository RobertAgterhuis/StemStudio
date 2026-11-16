using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Api;

/// <summary>Filters of <c>GET /api/generations</c> (backend contract 1.8.0); null members are not sent.</summary>
/// <param name="Kind">Only generations of this kind.</param>
/// <param name="SourceTrackId">Only restyles of this track.</param>
public sealed record GenerationQuery(GenerationKindDto? Kind = null, Guid? SourceTrackId = null);

/// <summary>Style library and generations (feature 009, backend contract 1.8.0).</summary>
public partial interface IStemStudioApiClient
{
    /// <summary><c>GET /api/generation/status</c>: whether the generator (and the singer, 1.9.0) can be used.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The availability with the reason when not ready.</returns>
    Task<GenerationStatusDto> GetGenerationStatusAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/styles</c>: every style, presets first, then by name.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The styles.</returns>
    Task<IReadOnlyList<StyleDto>> ListStylesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/styles/{id}</c>.</summary>
    /// <param name="styleId">The style.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The style; 404 throws <see cref="ApiException"/>.</returns>
    Task<StyleDto> GetStyleAsync(Guid styleId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/styles</c>: a new user style.</summary>
    /// <param name="input">The fields; every text passes the imitation guard.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The created style; 400 (field + reason) and 409 (name taken) throw <see cref="ApiException"/>.</returns>
    Task<StyleDto> CreateStyleAsync(StyleInput input, CancellationToken cancellationToken = default);

    /// <summary><c>PUT /api/styles/{id}</c>: replaces a user style.</summary>
    /// <param name="styleId">The user style.</param>
    /// <param name="input">The fields.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The updated style; 400, 403 (preset), 404 and 409 throw <see cref="ApiException"/>.</returns>
    Task<StyleDto> UpdateStyleAsync(Guid styleId, StyleInput input, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/styles/{id}/duplicate</c>: a user copy named "&lt;name&gt; (copy)".</summary>
    /// <param name="styleId">A preset or user style.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The copy.</returns>
    Task<StyleDto> DuplicateStyleAsync(Guid styleId, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/styles/{id}</c>: deletes a user style; earlier takes keep their snapshot.</summary>
    /// <param name="styleId">The user style.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the style is deleted (204); 403 and 404 throw <see cref="ApiException"/>.</returns>
    Task DeleteStyleAsync(Guid styleId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/tracks/{id}/restyles</c>: restyles a completed track.</summary>
    /// <param name="trackId">The completed track.</param>
    /// <param name="request">The choices; null members take the defaults.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The queued generation (202); 400, 404, 409 and 503 throw <see cref="ApiException"/>.</returns>
    Task<GenerationDto> CreateRestyleAsync(Guid trackId, RestyleRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/generations</c>: generations, newest first.</summary>
    /// <param name="query">The filters.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The generations.</returns>
    Task<IReadOnlyList<GenerationDto>> ListGenerationsAsync(GenerationQuery query, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/generations/{id}</c>.</summary>
    /// <param name="generationId">The generation.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The generation.</returns>
    Task<GenerationDto> GetGenerationAsync(Guid generationId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/generations/{id}/cancel</c>: cancels a queued or running generation.</summary>
    /// <param name="generationId">The generation.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The generation (cancelled, or already terminal).</returns>
    Task<GenerationDto> CancelGenerationAsync(Guid generationId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/generations/{id}/retry</c>: a new generation with new seeds.</summary>
    /// <param name="generationId">A failed or cancelled generation.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new generation (202); 409 throws <see cref="ApiException"/>.</returns>
    Task<GenerationDto> RetryGenerationAsync(Guid generationId, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/generations/{id}</c>: removes the takes and their library items; imported tracks stay.</summary>
    /// <param name="generationId">The generation.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the generation is deleted (204).</returns>
    Task DeleteGenerationAsync(Guid generationId, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/generations/{id}/takes/{index}/import</c>: imports a take as a track (idempotent).</summary>
    /// <param name="generationId">A completed generation.</param>
    /// <param name="index">The take number from 1.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new (202) or existing (200) track; 409 throws <see cref="ApiException"/>.</returns>
    Task<TrackDto> ImportTakeAsync(Guid generationId, int index, CancellationToken cancellationToken = default);
}
