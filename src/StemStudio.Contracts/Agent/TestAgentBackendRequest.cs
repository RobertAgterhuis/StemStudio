namespace StemStudio.Contracts.Agent;

/// <summary>Body of POST /api/agent/settings/test; never generates text.</summary>
public sealed record TestAgentBackendRequest
{
    /// <summary>Backend to test: ollama or claude (claude only on this explicit call).</summary>
    public required AgentBackendDto Backend { get; init; }
}
