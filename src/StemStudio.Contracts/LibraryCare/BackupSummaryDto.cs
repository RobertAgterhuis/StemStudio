namespace StemStudio.Contracts.LibraryCare;

/// <summary>
/// One archive in the backups folder with its manifest summary (<c>BackupSummary</c>, backend-api 1.12.0, feature 013).
/// A file without a readable manifest has <see cref="Recognised"/> false and null manifest fields.
/// </summary>
public record BackupSummaryDto
{
    /// <summary>File name (<see cref="LibraryCareLimits.ArchiveFileNamePattern"/>).</summary>
    public required string FileName { get; init; }

    /// <summary>File size in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Last write time of the file.</summary>
    public required DateTimeOffset ModifiedAt { get; init; }

    /// <summary>True when the file is a StemStudio backup with a readable manifest.</summary>
    public required bool Recognised { get; init; }

    /// <summary>Why the file is not recognised (no manifest, not a ZIP).</summary>
    public required string? Reason { get; init; }

    /// <summary>Manifest <c>backupId</c>.</summary>
    public required Guid? BackupId { get; init; }

    /// <summary>When the backup was made.</summary>
    public required DateTimeOffset? CreatedAt { get; init; }

    /// <summary>StemStudio version that made it.</summary>
    public required string? AppVersion { get; init; }

    /// <summary>Backend API version that made it.</summary>
    public required string? BackendApi { get; init; }

    /// <summary>Archive format version (1.0.0).</summary>
    public required string? FormatVersion { get; init; }

    /// <summary>Included categories.</summary>
    public required IReadOnlyList<BackupCategoryDto> Included { get; init; }

    /// <summary>Item counts; null when not recognised.</summary>
    public required ArchiveCountsDto? Counts { get; init; }

    /// <summary>Whether this version can restore it.</summary>
    public required CompatibilityDto Compatibility { get; init; }
}
