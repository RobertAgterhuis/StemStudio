namespace StemStudio.Contracts.LibraryCare;

/// <summary>A warning of an archive or a backup (<c>ArchiveWarning</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record ArchiveWarningDto
{
    /// <summary>Warning code.</summary>
    public required ArchiveWarningCodeDto Code { get; init; }

    /// <summary>Readable message (&lt;= 500 characters).</summary>
    public required string Message { get; init; }
}
