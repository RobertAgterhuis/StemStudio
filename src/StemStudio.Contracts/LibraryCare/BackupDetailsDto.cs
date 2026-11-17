namespace StemStudio.Contracts.LibraryCare;

/// <summary>Details of one archive (<c>BackupDetails</c> = <c>BackupSummary</c> plus the manifest details; backend-api 1.12.0, feature 013).</summary>
public sealed record BackupDetailsDto : BackupSummaryDto
{
    /// <summary>Excluded optional categories.</summary>
    public required IReadOnlyList<BackupCategoryDto> Excluded { get; init; }

    /// <summary>Number of archive entries.</summary>
    public required int Entries { get; init; }

    /// <summary>Uncompressed bytes of every entry.</summary>
    public required long TotalBytes { get; init; }

    /// <summary>Last database migration in the archive.</summary>
    public required string SchemaLastMigration { get; init; }

    /// <summary>Consent proofs of the voice profiles in the archive.</summary>
    public required IReadOnlyList<VoiceProfileProofDto> VoiceProfiles { get; init; }

    /// <summary>Warnings recorded in the manifest.</summary>
    public required IReadOnlyList<ArchiveWarningDto> Warnings { get; init; }
}
