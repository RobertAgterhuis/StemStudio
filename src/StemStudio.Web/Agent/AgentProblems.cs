using System.Text.Json;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Api;

namespace StemStudio.Web.Agent;

/// <summary>Reads agent-specific members of a mapped problem (backend API 1.10.0).</summary>
public static class AgentProblems
{
    /// <summary>The problem extension member that carries the <see cref="AgentStatusDto"/> of a <c>503 backend-unavailable</c>.</summary>
    public const string AgentStatusMember = "agentStatus";

    /// <summary>Reads the agent status that a <c>503 backend-unavailable</c> problem carries.</summary>
    /// <param name="error">The mapped error.</param>
    /// <returns>The status, or null when the problem has none.</returns>
    public static AgentStatusDto? StatusOf(ApiError error) =>
        error.Extensions.TryGetValue(AgentStatusMember, out var status)
            ? status.Deserialize<AgentStatusDto>(AgentApiClient.Json)
            : null;
}
