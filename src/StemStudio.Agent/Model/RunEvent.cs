namespace StemStudio.Agent.Model;

/// <summary>One persisted SSE event (data-model 4.5); Seq is the SSE id.</summary>
public sealed record RunEvent
{
    /// <summary>Run identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Sequence from 1 within the run.</summary>
    public required int Seq { get; init; }

    /// <summary>Event type, see AgentEventTypes.</summary>
    public required string Type { get; init; }

    /// <summary>JSON data, at most 16 KB.</summary>
    public required string Payload { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
