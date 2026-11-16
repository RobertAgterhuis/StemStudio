namespace StemStudio.Contracts.Agent;

/// <summary>Data of the text.delta event (coalesced per 100 ms).</summary>
public sealed record TextDeltaEventDto
{
    /// <summary>Streamed answer text to append.</summary>
    public required string Text { get; init; }
}
