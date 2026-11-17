namespace StemStudio.Contracts.Practice;

/// <summary>Practice summary of a track (<c>PracticeSummary</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record PracticeSummaryDto
{
    /// <summary>Track.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Today (local).</summary>
    public required double TodaySeconds { get; init; }

    /// <summary>Last 7 local days incl. today.</summary>
    public required double Last7DaysSeconds { get; init; }

    /// <summary>All time.</summary>
    public required double TotalSeconds { get; init; }

    /// <summary>Sessions all time.</summary>
    public required int Sessions { get; init; }

    /// <summary>Last session end, or null.</summary>
    public required DateTimeOffset? LastPractisedAt { get; init; }

    /// <summary>One entry per local day of the window.</summary>
    public required IReadOnlyList<PracticeDayDto> Days { get; init; }

    /// <summary>Per-section totals.</summary>
    public required IReadOnlyList<PracticeSectionTotalDto> Sections { get; init; }

    /// <summary>Trainer speed points.</summary>
    public required IReadOnlyList<PracticeTempoPointDto> Tempo { get; init; }
}
