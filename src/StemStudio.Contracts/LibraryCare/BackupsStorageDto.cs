namespace StemStudio.Contracts.LibraryCare;

/// <summary>Summary of the backups folder in the storage overview (<c>StorageOverview.backups</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record BackupsStorageDto
{
    /// <summary>Number of archives.</summary>
    public required int Count { get; init; }

    /// <summary>Bytes of the archives.</summary>
    public required long Bytes { get; init; }

    /// <summary>The folder is available.</summary>
    public required bool FolderAvailable { get; init; }

    /// <summary>Why it is not available.</summary>
    public required string? Reason { get; init; }
}
