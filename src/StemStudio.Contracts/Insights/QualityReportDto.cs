namespace StemStudio.Contracts.Insights;

/// <summary>The stem quality report of a track (<c>getQualityReport</c>) (backend-api 1.19.0, feature 020).</summary>
public sealed record QualityReportDto
{
    /// <summary>Status.</summary>
    public required QualityReportStatusDto Status { get; init; }

    /// <summary>Progress 0..100.</summary>
    public required int Progress { get; init; }

    /// <summary>Failure text; null otherwise.</summary>
    public required string? Error { get; init; }

    /// <summary>Analyzer version of the stems shown; null when none.</summary>
    public required string? AnalyzerVersion { get; init; }

    /// <summary>When the stems shown were measured; null when none.</summary>
    public required DateTimeOffset? MeasuredAt { get; init; }

    /// <summary>Made with an older analyzer version or before the stems changed.</summary>
    public required bool Outdated { get; init; }

    /// <summary>Fixed estimate disclaimer (<see cref="ContractLimits.QualityDisclaimer"/>).</summary>
    public required string Disclaimer { get; init; }

    /// <summary>Counts per rating.</summary>
    public required QualitySummaryDto Summary { get; init; }

    /// <summary>Stems of the last ready report (kept while a new one runs or after a failure).</summary>
    public required IReadOnlyList<StemQualityViewDto> Stems { get; init; }
}
