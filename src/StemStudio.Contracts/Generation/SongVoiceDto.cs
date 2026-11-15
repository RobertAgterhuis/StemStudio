namespace StemStudio.Contracts.Generation;

/// <summary>Vocal options of a song (1.9.0).</summary>
public sealed record SongVoiceDto
{
    /// <summary>Lead voice.</summary>
    public required LeadVoiceDto LeadVoice { get; init; }

    /// <summary>Delivery words, at most 40 characters.</summary>
    public string? Delivery { get; init; }
}
