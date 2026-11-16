using System.Text.Json;
using StemStudio.Contracts.Agent;

namespace StemStudio.Web.Agent;

/// <summary>One event of a run's stream (DELTA.md section 3): its sequence number, type and JSON data.</summary>
/// <param name="Id">The run-wide event sequence number (<c>id:</c>), used to resume with <c>Last-Event-ID</c>.</param>
/// <param name="Type">The event type, one of <see cref="AgentEventTypes"/>.</param>
/// <param name="Data">The JSON data.</param>
public sealed record AgentEvent(int Id, string Type, JsonElement Data)
{
    /// <summary>True for <c>run.completed</c>, <c>run.failed</c>, <c>run.cancelled</c> and <c>run.interrupted</c>.</summary>
    public bool IsTerminal => AgentEventTypes.IsTerminal(Type);

    /// <summary>Reads the data as a contract DTO.</summary>
    /// <typeparam name="T">The DTO type of this event type.</typeparam>
    /// <returns>The DTO.</returns>
    /// <exception cref="JsonException">The data is null or does not match.</exception>
    public T Read<T>() => Data.Deserialize<T>(AgentApiClient.Json) ?? throw new JsonException($"The {Type} event has no data.");
}
