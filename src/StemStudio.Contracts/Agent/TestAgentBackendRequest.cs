using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Body of POST /api/agent/settings/test; never generates text.</summary>
public sealed record TestAgentBackendRequest
{
    /// <summary>Backend to test: ollama or claude (claude only on this explicit call).</summary>
    public required AgentBackendDto Backend { get; init; }

    /// <summary>Claude only (1.21.0): which key to test; omitted means <see cref="ClaudeKeyTestSourceDto.Active"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClaudeKeyTestSourceDto? ClaudeKeySource { get; init; }
}
