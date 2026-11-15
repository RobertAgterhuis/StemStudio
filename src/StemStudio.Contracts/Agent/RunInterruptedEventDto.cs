namespace StemStudio.Contracts.Agent;

/// <summary>Data of the run.interrupted event.</summary>
public sealed record RunInterruptedEventDto
{
    /// <summary>Run identifier.</summary>
    public required Guid RunId { get; init; }
}
