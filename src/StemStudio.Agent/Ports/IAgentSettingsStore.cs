using StemStudio.Agent.Model;

namespace StemStudio.Agent.Ports;

/// <summary>The single agent settings row (data-model 2). Implemented by the backend (EfAgentSettingsStore, T021).</summary>
public interface IAgentSettingsStore
{
    /// <summary>Loads the settings, or <see cref="AgentSettings.Defaults"/> when no row exists yet.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The settings.</returns>
    Task<AgentSettings> GetAsync(CancellationToken cancellationToken);

    /// <summary>Stores the settings (validation happens before, in the settings endpoint).</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(AgentSettings settings, CancellationToken cancellationToken);
}
