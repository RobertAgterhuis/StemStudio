namespace StemStudio.Contracts.LibraryCare;

/// <summary>Report of a completed restore or undo (<c>RestoreReport</c>, <c>type: restore</c>; backend-api 1.12.0, feature 013).</summary>
public sealed record RestoreReportDto : LibraryJobReportDto
{
    /// <summary>What the restore did.</summary>
    public required RestoreReportModeDto Mode { get; init; }

    /// <summary>Items, files and bytes per restored category.</summary>
    public required IReadOnlyList<CategoryCountDto> Restored { get; init; }

    /// <summary>Items not restored (or renamed) and why.</summary>
    public required IReadOnlyList<SkippedItemDto> Skipped { get; init; }

    /// <summary>Last migration of the archive when it was migrated; null otherwise.</summary>
    public required string? MigratedFrom { get; init; }

    /// <summary>Readable warnings (each &lt;= 500 characters).</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>True when the previous library was kept aside (undo possible).</summary>
    public required bool PreviousLibraryKept { get; init; }
}
