using StemStudio.Domain.Voices;

namespace StemStudio.Domain.LibraryCare;

/// <summary>The consent proof of an archived voice profile: the five fields that manifest and database must agree on.</summary>
/// <param name="StatementVersion">The statement version agreed to.</param>
/// <param name="StatementSha256">SHA-256 of that statement text.</param>
/// <param name="PersonName">The typed person name.</param>
/// <param name="ConsentedAt">When (UTC).</param>
/// <param name="SampleSha256">SHA-256 of the sample file.</param>
public sealed record ConsentProof(string StatementVersion, string StatementSha256, string PersonName, DateTime ConsentedAt, string SampleSha256)
{
    /// <summary>The proof of a stored consent record and sample hash.</summary>
    /// <param name="consent">The consent record.</param>
    /// <param name="sampleSha256">SHA-256 of the sample file.</param>
    /// <returns>The proof.</returns>
    public static ConsentProof From(VoiceConsent consent, string sampleSha256)
    {
        ArgumentNullException.ThrowIfNull(consent);
        return new ConsentProof(consent.StatementVersion, consent.StatementSha256, consent.PersonName, consent.ConsentedAt, sampleSha256);
    }
}

/// <summary>Why a consent proof is not valid (checked in this order).</summary>
public enum ConsentProofProblem
{
    /// <summary>Valid.</summary>
    None,

    /// <summary>The statement version is not embedded in this StemStudio.</summary>
    UnknownStatement,

    /// <summary>The stored statement hash differs from the embedded text.</summary>
    StatementMismatch,

    /// <summary>The person name is not 1-80 characters after trimming.</summary>
    PersonName,

    /// <summary>The consent is dated after the archive.</summary>
    ConsentedAfterArchive,

    /// <summary>The sample file is not in the archive.</summary>
    SampleMissing,

    /// <summary>The archived sample hash differs from the proof.</summary>
    SampleMismatch,

    /// <summary>The database record differs from the manifest proof (or is missing).</summary>
    DatabaseDiffers,
}

/// <summary>Consent proof rules of a backup and a restore (013 data-model 5.4, ADR-019 D3, ADR-022 D7).</summary>
public static class ConsentProofRules
{
    /// <summary>True when the statement version is embedded and its hash equals the stored hash.</summary>
    /// <param name="version">Stored statement version.</param>
    /// <param name="sha256">Stored statement hash.</param>
    /// <returns>True when the statement is verifiable.</returns>
    public static bool IsStatementValid(string version, string sha256) =>
        ConsentStatement.Known(version) is { } known && string.Equals(known.Sha256, sha256, StringComparison.Ordinal);

    /// <summary>True when a stored consent record may be archived: a verifiable statement and a person name of 1-80 characters.</summary>
    /// <param name="consent">The record, or null.</param>
    /// <returns>True when complete.</returns>
    public static bool IsComplete(VoiceConsent? consent) =>
        consent is not null && IsStatementValid(consent.StatementVersion, consent.StatementSha256) && IsValidName(consent.PersonName);

    /// <summary>Checks a proof of a restore.</summary>
    /// <param name="manifest">The proof in the manifest.</param>
    /// <param name="database">The proof built from the archived database row, or null when the row is missing.</param>
    /// <param name="archiveCreatedAt">The manifest's creation time.</param>
    /// <param name="archivedSampleSha256">SHA-256 of the sample entry in the archive, or null when absent.</param>
    /// <returns>The first problem, or <see cref="ConsentProofProblem.None"/>.</returns>
    public static ConsentProofProblem Check(ConsentProof manifest, ConsentProof? database, DateTime archiveCreatedAt, string? archivedSampleSha256)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return StatementProblem(manifest)
            ?? FieldProblem(manifest, archiveCreatedAt)
            ?? SampleProblem(manifest, archivedSampleSha256)
            ?? (SameProof(manifest, database) ? ConsentProofProblem.None : ConsentProofProblem.DatabaseDiffers);
    }

    /// <summary>A readable reason for a report.</summary>
    /// <param name="problem">The problem.</param>
    /// <returns>The reason, or null for <see cref="ConsentProofProblem.None"/>.</returns>
    public static string? Reason(ConsentProofProblem problem) => problem switch
    {
        ConsentProofProblem.None => null,
        ConsentProofProblem.UnknownStatement => "The consent statement version is unknown to this StemStudio.",
        ConsentProofProblem.StatementMismatch => "The consent statement does not match the agreed text.",
        ConsentProofProblem.PersonName => "The consent has no valid person name.",
        ConsentProofProblem.ConsentedAfterArchive => "The consent is dated after the backup.",
        ConsentProofProblem.SampleMissing => "The voice sample is missing from the backup.",
        ConsentProofProblem.SampleMismatch => "The voice sample does not match its recorded hash.",
        _ => "The consent record in the database differs from the backup's proof.",
    };

    private static ConsentProofProblem? StatementProblem(ConsentProof proof)
    {
        if (ConsentStatement.Known(proof.StatementVersion) is not { } known)
        {
            return ConsentProofProblem.UnknownStatement;
        }

        return string.Equals(known.Sha256, proof.StatementSha256, StringComparison.Ordinal) ? null : ConsentProofProblem.StatementMismatch;
    }

    private static ConsentProofProblem? FieldProblem(ConsentProof proof, DateTime archiveCreatedAt)
    {
        if (!IsValidName(proof.PersonName))
        {
            return ConsentProofProblem.PersonName;
        }

        return proof.ConsentedAt <= archiveCreatedAt ? null : ConsentProofProblem.ConsentedAfterArchive;
    }

    private static ConsentProofProblem? SampleProblem(ConsentProof proof, string? archivedSampleSha256) => archivedSampleSha256 switch
    {
        null => ConsentProofProblem.SampleMissing,
        _ when !string.Equals(archivedSampleSha256, proof.SampleSha256, StringComparison.Ordinal) => ConsentProofProblem.SampleMismatch,
        _ => null,
    };

    private static bool SameProof(ConsentProof manifest, ConsentProof? database) =>
        database is not null && manifest with { PersonName = manifest.PersonName.Trim() } == database with { PersonName = database.PersonName.Trim() };

    private static bool IsValidName(string name) => name.Trim().Length is > 0 and <= ConsentRules.MaxPersonNameLength;
}
