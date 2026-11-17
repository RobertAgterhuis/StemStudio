namespace StemStudio.Contracts.LibraryCare;

/// <summary>State of library care (<c>LibraryCareStatus</c>); also answered during maintenance (backend-api 1.12.0, feature 013).</summary>
public sealed record LibraryCareStatusDto
{
    /// <summary>Mode of the write gate.</summary>
    public required GateModeDto Gate { get; init; }

    /// <summary>The queued or running library job; null when none.</summary>
    public required LibraryJobDto? ActiveJob { get; init; }

    /// <summary>State of the backups folder.</summary>
    public required FolderStateDto BackupFolder { get; init; }

    /// <summary>The library kept aside by a replace restore; null when none.</summary>
    public required PreviousLibraryDto? PreviousLibrary { get; init; }

    /// <summary>Queued or running jobs that block a restore.</summary>
    public required IReadOnlyList<ActiveWorkDto> ActiveWork { get; init; }

    /// <summary>True when a restore needs no replace (research R-07).</summary>
    public required bool LibraryEmpty { get; init; }
}
