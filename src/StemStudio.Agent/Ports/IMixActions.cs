using StemStudio.Contracts.Mixes;

namespace StemStudio.Agent.Ports;

/// <summary>Mix endpoints of the public API (feature 001) over loopback. Failures are <see cref="AgentActionException"/>.</summary>
public interface IMixActions
{
    /// <summary>GET /api/mixes (optionally <c>?trackId=</c>).</summary>
    /// <param name="trackId">Only mixes of this track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Mixes, newest first.</returns>
    Task<IReadOnlyList<MixSummaryDto>> ListMixesAsync(Guid? trackId, CancellationToken cancellationToken);

    /// <summary>GET /api/mixes/{mixId}.</summary>
    /// <param name="mixId">Mix identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The mix.</returns>
    Task<MixDto> GetMixAsync(Guid mixId, CancellationToken cancellationToken);

    /// <summary>POST /api/mixes (save_mix_as).</summary>
    /// <param name="request">Track and settings.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The new mix.</returns>
    Task<MixDto> CreateMixAsync(CreateMixRequest request, CancellationToken cancellationToken);

    /// <summary>PUT /api/mixes/{mixId} (update_mix, rename_mix).</summary>
    /// <param name="mixId">Mix identifier.</param>
    /// <param name="settings">Complete new settings (the undo entry keeps the previous version).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The updated mix.</returns>
    Task<MixDto> UpdateMixAsync(Guid mixId, MixSettingsDto settings, CancellationToken cancellationToken);

    /// <summary>POST /api/mixes/{mixId}/duplicate.</summary>
    /// <param name="mixId">Mix identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The copy, named "&lt;name&gt; (copy)".</returns>
    Task<MixDto> DuplicateMixAsync(Guid mixId, CancellationToken cancellationToken);

    /// <summary>DELETE /api/mixes/{mixId} (destructive).</summary>
    /// <param name="mixId">Mix identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteMixAsync(Guid mixId, CancellationToken cancellationToken);
}
