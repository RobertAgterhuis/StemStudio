namespace StemStudio.Contracts.Agent;

/// <summary>Short form of a run in a conversation.</summary>
public sealed record AgentRunSummaryDto
{
    /// <summary>Run identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>The user message (command) of the run.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>Run status.</summary>
    public required AgentRunStatusDto Status { get; init; }

    /// <summary>Start (UTC).</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>End (UTC) of a terminal run.</summary>
    public DateTimeOffset? EndedAt { get; init; }
}
