namespace StemStudio.Contracts.Practice;

/// <summary>Result of a bulk delete (<c>DeletedCount</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record DeletedCountDto
{
    /// <summary>Number of deleted sessions.</summary>
    public required int Deleted { get; init; }
}
