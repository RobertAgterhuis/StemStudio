namespace StemStudio.Contracts.Agent;

/// <summary>202 body of a sent message: the stored message and the queued run.</summary>
public sealed record AgentMessageAcceptedDto
{
    /// <summary>Run that executes the command.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Stored user message.</summary>
    public required Guid MessageId { get; init; }
}
