namespace StemStudio.Contracts.Practice;

/// <summary>One row of the practice overview (<c>PracticeTrackTotal</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record PracticeTrackTotalDto
{
    /// <summary>Track.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Track title.</summary>
    public required string Title { get; init; }

    /// <summary>Artist, or null.</summary>
    public required string? Artist { get; init; }

    /// <summary>All time.</summary>
    public required double TotalSeconds { get; init; }

    /// <summary>Last 7 local days.</summary>
    public required double Last7DaysSeconds { get; init; }

    /// <summary>Sessions.</summary>
    public required int Sessions { get; init; }

    /// <summary>Last session end.</summary>
    public required DateTimeOffset LastPractisedAt { get; init; }
}
