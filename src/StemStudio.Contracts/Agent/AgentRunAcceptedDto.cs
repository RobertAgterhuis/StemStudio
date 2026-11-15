namespace StemStudio.Contracts.Agent;

/// <summary>202 body of an undo request: the queued undo run.</summary>
public sealed record AgentRunAcceptedDto
{
    /// <summary>Run that executes the recorded inverse actions.</summary>
    public required Guid RunId { get; init; }
}
