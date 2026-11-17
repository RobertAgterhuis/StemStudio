namespace StemStudio.Contracts.LibraryCare;

/// <summary>The library moved aside by a replace restore (<c>PreviousLibrary</c>, at most one) (backend-api 1.12.0, feature 013).</summary>
public sealed record PreviousLibraryDto
{
    /// <summary>Id of the restore that moved it aside.</summary>
    public required Guid RestoreId { get; init; }

    /// <summary>When it was moved aside.</summary>
    public required DateTimeOffset MovedAsideAt { get; init; }

    /// <summary>Size on disk.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Item counts.</summary>
    public required ArchiveCountsDto Counts { get; init; }

    /// <summary>File name of the archive that replaced it.</summary>
    public required string? RestoredArchive { get; init; }
}
