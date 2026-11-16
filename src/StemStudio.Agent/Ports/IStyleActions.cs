using StemStudio.Contracts.Styles;

namespace StemStudio.Agent.Ports;

/// <summary>Style library endpoints of the public API (feature 009) over loopback.</summary>
public interface IStyleActions
{
    /// <summary>GET /api/styles with filters (list_styles): presets first, then user styles.</summary>
    /// <param name="query">Filters.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The matching styles.</returns>
    Task<IReadOnlyList<StyleDto>> ListStylesAsync(StyleQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// POST /api/styles (create_style). The backend's imitation guard is authoritative: a hit surfaces as an
    /// <see cref="AgentActionException"/> with the readable refusal.
    /// </summary>
    /// <param name="input">The new user style.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The created style.</returns>
    Task<StyleDto> CreateStyleAsync(StyleInput input, CancellationToken cancellationToken);

    /// <summary>DELETE /api/styles/{styleId} (delete_style; compensation of create_style). Presets are read-only (409).</summary>
    /// <param name="styleId">Style identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteStyleAsync(Guid styleId, CancellationToken cancellationToken);
}
