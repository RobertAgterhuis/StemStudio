namespace StemStudio.Contracts.Generation;

/// <summary>Availability of the voice features, i.e. the singer service (1.9.0).</summary>
public sealed record VoiceServiceStatusDto
{
    /// <summary>Availability.</summary>
    public required GenerationAvailabilityDto State { get; init; }

    /// <summary>Readable reason when not ready.</summary>
    public string? Reason { get; init; }
}
