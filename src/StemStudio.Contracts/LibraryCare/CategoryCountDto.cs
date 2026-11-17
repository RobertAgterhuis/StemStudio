namespace StemStudio.Contracts.LibraryCare;

/// <summary>Items, files and bytes of one category in a report (<c>CategoryCount</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record CategoryCountDto
{
    /// <summary>The category.</summary>
    public required BackupCategoryDto Category { get; init; }

    /// <summary>Items (tracks, assets, ...).</summary>
    public required int Items { get; init; }

    /// <summary>Files.</summary>
    public required int Files { get; init; }

    /// <summary>Bytes.</summary>
    public required long Bytes { get; init; }
}
