namespace StemStudio.Contracts.LibraryCare;

/// <summary>Report of a completed backup (<c>BackupReport</c>, <c>type: backup</c>; backend-api 1.12.0, feature 013).</summary>
public sealed record BackupReportDto : LibraryJobReportDto
{
    /// <summary>File name of the written archive.</summary>
    public required string FileName { get; init; }

    /// <summary>Items, files and bytes per included category.</summary>
    public required IReadOnlyList<CategoryCountDto> Included { get; init; }

    /// <summary>Number of archive entries.</summary>
    public required int Entries { get; init; }

    /// <summary>Archive file size in bytes.</summary>
    public required long Bytes { get; init; }

    /// <summary>Warnings (missing files, changes after the snapshot, left-out voice profiles).</summary>
    public required IReadOnlyList<ArchiveWarningDto> Warnings { get; init; }
}
