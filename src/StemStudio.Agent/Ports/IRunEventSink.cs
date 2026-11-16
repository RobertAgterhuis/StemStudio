using StemStudio.Agent.Model;

namespace StemStudio.Agent.Ports;

/// <summary>
/// Append-only event log of a run (data-model 4.5). The backend implementation persists the event, assigns the next Seq and
/// pushes it to live SSE subscribers; replay (after / Last-Event-ID) reads the persisted log.
/// </summary>
public interface IRunEventSink
{
    /// <summary>Appends and broadcasts an event.</summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="type">Event type, see <c>AgentEventTypes</c>.</param>
    /// <param name="payload">JSON data (the DTO of the event type, camelCase), at most 16 KB.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The stored event with its Seq.</returns>
    Task<RunEvent> PublishAsync(Guid runId, string type, string payload, CancellationToken cancellationToken);
}
