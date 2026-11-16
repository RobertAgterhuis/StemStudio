using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Agent.Ports;

/// <summary>
/// Generation endpoints of the public API (features 009 restyles, 010 songs and voice conversions) over loopback.
/// A <see cref="GenerationDto"/> of a song carries the user's lyrics snapshot; the result projections of the tool catalogue
/// never include it.
/// </summary>
public interface IGenerationActions
{
    /// <summary>GET /api/generation/status (get_generation_status; availability of the <c>generation</c> and <c>voice</c> services).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The status.</returns>
    Task<GenerationStatusDto> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>POST /api/tracks/{trackId}/restyles (restyle_track, heavy GPU job).</summary>
    /// <param name="trackId">Source track.</param>
    /// <param name="request">Style, range, stems, strength and takes.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued generation.</returns>
    Task<GenerationDto> CreateRestyleAsync(Guid trackId, RestyleRequest request, CancellationToken cancellationToken);

    /// <summary>GET /api/generations with filters (list_generations).</summary>
    /// <param name="query">Filters.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Generations, newest first.</returns>
    Task<IReadOnlyList<GenerationDto>> ListGenerationsAsync(GenerationQuery query, CancellationToken cancellationToken);

    /// <summary>GET /api/generations/{generationId} (job status for wait_for_job, phases restyle and generation).</summary>
    /// <param name="generationId">Generation identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The generation.</returns>
    Task<GenerationDto> GetGenerationAsync(Guid generationId, CancellationToken cancellationToken);

    /// <summary>POST /api/generations/{generationId}/cancel (cancel_generation; first part of every generation compensation).</summary>
    /// <param name="generationId">Generation identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The generation (a terminal one unchanged).</returns>
    Task<GenerationDto> CancelGenerationAsync(Guid generationId, CancellationToken cancellationToken);

    /// <summary>POST /api/generations/{generationId}/retry (retry_generation, heavy).</summary>
    /// <param name="generationId">Generation identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued generation.</returns>
    Task<GenerationDto> RetryGenerationAsync(Guid generationId, CancellationToken cancellationToken);

    /// <summary>DELETE /api/generations/{generationId} (delete_generation; compensation of restyles, songs and conversions).</summary>
    /// <param name="generationId">Generation identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteGenerationAsync(Guid generationId, CancellationToken cancellationToken);

    /// <summary>POST /api/generations/{generationId}/takes/{index}/import (import_take): the take becomes a track and is separated.</summary>
    /// <param name="generationId">Generation identifier.</param>
    /// <param name="index">Take number, from 1.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The new (or already imported) track.</returns>
    Task<TrackDto> ImportTakeAsync(Guid generationId, int index, CancellationToken cancellationToken);
}
