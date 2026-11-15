namespace StemStudio.Contracts.Agent;

/// <summary>Data of the run.completed event.</summary>
public sealed record RunCompletedEventDto
{
    /// <summary>Run identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Stored assistant message with the final answer.</summary>
    public required Guid MessageId { get; init; }
}
