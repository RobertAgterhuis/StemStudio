using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Ports;

/// <summary>Inbound port for the status, settings test and tool list endpoints. Implemented by the agent library.</summary>
public interface IAgentStatusService
{
    /// <summary>Probes the selected backend without generating text (each probe at most 2 s).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The status (never the key).</returns>
    Task<AgentStatusDto> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>Tests one backend on the user's explicit request (Claude: <c>GET /v1/models/{id}</c> only, no generation).</summary>
    /// <param name="backend">ollama or claude.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The test result.</returns>
    Task<BackendTestResultDto> TestAsync(AgentBackendDto backend, CancellationToken cancellationToken);

    /// <summary>
    /// Tests one backend on the user's explicit request with the chosen Claude key (backend-api 1.21.0,
    /// <c>TestAgentBackendRequest.claudeKeySource</c>); the source is ignored for backends other than Claude.
    /// </summary>
    /// <param name="backend">ollama or claude.</param>
    /// <param name="claudeKeySource">Which Claude key to test: active (default), environment or stored.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The test result; for Claude <c>keySource</c> names the tested key (null when none was usable).</returns>
    Task<BackendTestResultDto> TestAsync(AgentBackendDto backend, ClaudeKeyTestSourceDto claudeKeySource, CancellationToken cancellationToken);

    /// <summary>The registry view for a page context (available and unavailable tools, sorted by name).</summary>
    /// <param name="context">Page context.</param>
    /// <returns>The tools.</returns>
    IReadOnlyList<AgentToolInfoDto> ListTools(PageContextDto context);
}
