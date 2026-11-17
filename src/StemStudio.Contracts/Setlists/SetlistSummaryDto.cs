namespace StemStudio.Contracts.Setlists;

/// <summary>A setlist in the list (<c>SetlistSummary</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record SetlistSummaryDto
{
    /// <summary>Setlist id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Name.</summary>
    public required string Name { get; init; }

    /// <summary>Number of entries.</summary>
    public required int Songs { get; init; }

    /// <summary>Total length.</summary>
    public required double TotalSeconds { get; init; }

    /// <summary>Last change.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
