namespace StemStudio.Contracts.Agent;

/// <summary>Data of the model.changed event.</summary>
public sealed record ModelChangedEventDto
{
    /// <summary>Model used from now on.</summary>
    public required string Model { get; init; }

    /// <summary>True when it is the CPU fallback model.</summary>
    public required bool UsingFallback { get; init; }

    /// <summary>Readable reason, e.g. "A GPU job started".</summary>
    public required string Reason { get; init; }
}
