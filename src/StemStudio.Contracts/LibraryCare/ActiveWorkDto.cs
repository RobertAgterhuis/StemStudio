namespace StemStudio.Contracts.LibraryCare;

/// <summary>A queued or running job that blocks a restore (<c>ActiveWork</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record ActiveWorkDto
{
    /// <summary>Kind of job.</summary>
    public required ActiveWorkKindDto Kind { get; init; }

    /// <summary>Id of the job's item (&lt;= 64 characters).</summary>
    public required string Id { get; init; }

    /// <summary>Display title (&lt;= 300 characters).</summary>
    public required string Title { get; init; }
}
