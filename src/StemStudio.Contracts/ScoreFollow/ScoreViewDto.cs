namespace StemStudio.Contracts.ScoreFollow;

/// <summary>The score view of a score asset (<c>ScoreView</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record ScoreViewDto
{
    /// <summary>Score asset id.</summary>
    public required Guid AssetId { get; init; }

    /// <summary>State.</summary>
    public required ScoreViewStatusDto Status { get; init; }

    /// <summary>Source track of the score, or null.</summary>
    public required Guid? TrackId { get; init; }

    /// <summary>Track time of the score's input start (already added to the bar times).</summary>
    public required double OffsetSeconds { get; init; }

    /// <summary>Pages (empty until ready).</summary>
    public required IReadOnlyList<ScoreViewPageDto> Pages { get; init; }

    /// <summary>Bars (empty until ready).</summary>
    public required IReadOnlyList<ScoreBarDto> Bars { get; init; }

    /// <summary>Reason when failed.</summary>
    public required string? Error { get; init; }
}
