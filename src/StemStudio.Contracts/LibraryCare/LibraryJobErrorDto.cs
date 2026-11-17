namespace StemStudio.Contracts.LibraryCare;

/// <summary>Error of a failed library job (<c>JobError</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record LibraryJobErrorDto
{
    /// <summary>Error code.</summary>
    public required LibraryJobErrorCodeDto Code { get; init; }

    /// <summary>Readable message without file paths (&lt;= 500 characters).</summary>
    public required string Message { get; init; }
}
