namespace StemStudio.Contracts.Insights;

/// <summary><c>Track.quality</c>: the summary badge of the quality report (backend-api 1.19.0, feature 020).</summary>
public sealed record QualityBadgeDto
{
    /// <summary>Status.</summary>
    public required QualityReportStatusDto Status { get; init; }

    /// <summary>Counts per rating.</summary>
    public required QualitySummaryDto Summary { get; init; }

    /// <summary>The report is outdated.</summary>
    public required bool Outdated { get; init; }
}
