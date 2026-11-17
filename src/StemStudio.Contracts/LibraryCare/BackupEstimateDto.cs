namespace StemStudio.Contracts.LibraryCare;

/// <summary>Estimate of a backup with the given options (<c>BackupEstimate</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record BackupEstimateDto
{
    /// <summary>Uncompressed size of the archive (audio is stored, so the archive is about this size).</summary>
    public required long TotalBytes { get; init; }

    /// <summary>Size per category.</summary>
    public required IReadOnlyList<CategorySizeDto> Categories { get; init; }

    /// <summary>State of the backups folder.</summary>
    public required FolderStateDto Folder { get; init; }

    /// <summary>Free space &gt;= <see cref="TotalBytes"/> + 64 MiB.</summary>
    public required bool Enough { get; init; }
}
