namespace StemStudio.Contracts.LibraryCare;

/// <summary>The backups folder and its archives, newest first (<c>BackupList</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record BackupListDto
{
    /// <summary>State of the backups folder.</summary>
    public required FolderStateDto Folder { get; init; }

    /// <summary>Every <c>*.zip</c> file in the folder (never <c>*.partial</c>).</summary>
    public required IReadOnlyList<BackupSummaryDto> Archives { get; init; }
}
