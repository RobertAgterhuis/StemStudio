namespace StemStudio.Contracts.Agent;

/// <summary>Data of the run.started event.</summary>
public sealed record RunStartedEventDto
{
    /// <summary>Run identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Backend.</summary>
    public required AgentBackendDto Backend { get; init; }

    /// <summary>Model of the first turn.</summary>
    public required string Model { get; init; }

    /// <summary>True when the CPU fallback model is used.</summary>
    public required bool UsingFallback { get; init; }
}
