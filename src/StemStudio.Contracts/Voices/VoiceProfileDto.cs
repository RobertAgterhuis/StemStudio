namespace StemStudio.Contracts.Voices;

/// <summary>An own-voice profile (1.9.0); voice data never leaves this computer.</summary>
public sealed record VoiceProfileDto
{
    /// <summary>Profile id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Display name, 1-60 characters, unique.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Consent record.</summary>
    public required VoiceConsentDto Consent { get; init; }

    /// <summary>Recorded or uploaded.</summary>
    public required VoiceSampleSourceDto Source { get; init; }

    /// <summary>Sample length (10-60 s).</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Voiced share of the sample (>= 0.5).</summary>
    public required double VoicedRatio { get; init; }

    /// <summary>Relative URL of the normalised sample.</summary>
    public required string SampleUrl { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Takes converted to this voice.</summary>
    public int ConvertedTakeCount { get; init; }
}
