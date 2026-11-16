using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>Maps a <see cref="ClaudeKeyResolution"/> to the wire status <see cref="ClaudeKeyStatusDto"/> (backend API 1.21.0); never the key.</summary>
public static class ClaudeKeyStatus
{
    /// <summary>The status of a resolution (also usable by the key endpoints of the API).</summary>
    /// <param name="resolution">The resolution of <see cref="Ports.IClaudeKeyProvider.Resolve"/>.</param>
    /// <returns>The status: sources, states and last four characters only.</returns>
    public static ClaudeKeyStatusDto ToDto(ClaudeKeyResolution resolution) => resolution is null
        ? throw new ArgumentNullException(nameof(resolution))
        : new ClaudeKeyStatusDto
        {
            Configured = resolution.Configured,
            Source = resolution.Source,
            State = resolution.State,
            LastFour = resolution.LastFour,
            Stored = resolution.Stored,
            StoredLastFour = resolution.StoredLastFour,
            StoredAt = resolution.StoredAt,
            StoreAvailable = resolution.StoreAvailable,
            Reason = resolution.Reason,
            Fix = resolution.Fix,
        };
}
