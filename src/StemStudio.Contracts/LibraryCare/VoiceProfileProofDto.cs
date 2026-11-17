namespace StemStudio.Contracts.LibraryCare;

/// <summary>Consent proof of a voice profile in an archive (<c>VoiceProfileProof</c>, 010 rules) (backend-api 1.12.0, feature 013).</summary>
public sealed record VoiceProfileProofDto
{
    /// <summary>Voice profile id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Profile name (&lt;= 60 characters).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Consent statement version, e.g. <c>consent.v1</c>.</summary>
    public required string StatementVersion { get; init; }

    /// <summary>Name of the consenting person (&lt;= 80 characters).</summary>
    public required string PersonName { get; init; }

    /// <summary>When consent was given.</summary>
    public required DateTimeOffset ConsentedAt { get; init; }

    /// <summary>Statement version known and statement SHA-256 matching (the full check runs during restore).</summary>
    public required bool Valid { get; init; }

    /// <summary>Why the proof is not valid.</summary>
    public required string? Reason { get; init; }
}
