namespace StemStudio.Contracts.Voices;

/// <summary>The stored consent of a voice profile (1.9.0).</summary>
public sealed record VoiceConsentDto
{
    /// <summary>Statement version (<c>consent.v1</c>).</summary>
    public required string StatementVersion { get; init; }

    /// <summary>Typed name of the person, 1-80 characters.</summary>
    public required string PersonName { get; init; }

    /// <summary>When consent was given (UTC).</summary>
    public required DateTimeOffset ConsentedAt { get; init; }
}
