using StemStudio.Contracts;

namespace StemStudio.Agent.Ports;

/// <summary>An own-voice profile without personal data or audio: what list_voice_profiles may show (FR-025, FR-026).</summary>
public sealed record VoiceProfileSummary
{
    /// <summary>Profile id.</summary>
    public required Guid ProfileId { get; init; }

    /// <summary>Display name (the user's own text; projected as <c>untrusted_text</c>).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Version of the consent statement the person agreed to.</summary>
    public required string ConsentStatementVersion { get; init; }

    /// <summary>Sample length in seconds.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>True when the consent was given to the current statement (otherwise a conversion returns 409 consent-outdated).</summary>
    public bool ConsentCurrent => ConsentStatementVersion == ContractLimits.ConsentStatementVersion;
}
