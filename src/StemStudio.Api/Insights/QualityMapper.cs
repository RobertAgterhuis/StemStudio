using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Insights;
using StemStudio.Domain.Insights;

namespace StemStudio.Api.Insights;

/// <summary>Maps the stored quality report to the contract (backend 1.19.0 <c>QualityReport</c>, <c>QualityBadge</c>).</summary>
public static class QualityMapper
{
    /// <summary>The report of a track that never had one (status <c>none</c>).</summary>
    public static QualityReportDto None { get; } = new()
    {
        Status = QualityReportStatusDto.None,
        Progress = 0,
        Error = null,
        AnalyzerVersion = null,
        MeasuredAt = null,
        Outdated = false,
        Disclaimer = ContractLimits.QualityDisclaimer,
        Summary = Count([]),
        Stems = [],
    };

    /// <summary>Maps a report.</summary>
    /// <param name="report">The stored report, or null.</param>
    /// <param name="outdated">Made with an older analyzer or before the stems changed.</param>
    /// <param name="tips">Tips per channel (absent = none).</param>
    /// <returns>The DTO.</returns>
    public static QualityReportDto ToDto(QualityReport? report, bool outdated, IReadOnlyDictionary<string, IReadOnlyList<TipDto>> tips)
    {
        ArgumentNullException.ThrowIfNull(tips);
        if (report is null)
        {
            return None;
        }

        return new QualityReportDto
        {
            Status = Status(report.Status),
            Progress = Math.Clamp(report.Progress, 0, 100),
            Error = report.Error,
            AnalyzerVersion = report.AnalyzerVersion,
            MeasuredAt = report.MeasuredAt is { } measured ? TrackMapper.Utc(measured) : null,
            Outdated = outdated,
            Disclaimer = ContractLimits.QualityDisclaimer,
            Summary = Count(report.Stems),
            Stems = report.Stems.Select(stem => ToDto(stem, report.Previous, tips.GetValueOrDefault(stem.Channel) ?? [])).ToList(),
        };
    }

    /// <summary>The badge of a track (<c>Track.quality</c>); null when never made.</summary>
    /// <param name="report">The stored report, or null.</param>
    /// <param name="outdated">The outdated flag.</param>
    /// <returns>The badge or null.</returns>
    public static QualityBadgeDto? Badge(QualityReport? report, bool outdated) => report is null
        ? null
        : new QualityBadgeDto { Status = Status(report.Status), Summary = Count(report.Stems), Outdated = outdated };

    /// <summary>The wire rating.</summary>
    /// <param name="rating">The rating.</param>
    /// <returns>The DTO value.</returns>
    public static QualityRatingDto ToDto(QualityRating rating) => Enum.Parse<QualityRatingDto>(rating.ToString());

    private static StemQualityViewDto ToDto(StemQuality stem, IReadOnlyDictionary<string, QualityRating> previous, IReadOnlyList<TipDto> tips) => new()
    {
        Channel = stem.Channel,
        Parent = stem.Channel.Contains('.', StringComparison.Ordinal) ? stem.Channel[..stem.Channel.IndexOf('.', StringComparison.Ordinal)] : null,
        Rating = ToDto(stem.Rating),
        Reason = QualityReasonTexts.For(stem),
        PreviousRating = previous.TryGetValue(stem.Channel, out var was) ? ToDto(was) : null,
        Bleed = new BleedViewDto
        {
            Level = Level(stem.Bleed.Level),
            Index = stem.Bleed.Index,
            MainSource = stem.Bleed.MainSource,
            CoOccurrence = stem.Bleed.CoOccurrence,
            Reliability = Reliability(stem.Bleed.Reliability),
        },
        Artefacts = new ArtefactViewDto
        {
            Level = Level(stem.Artefacts.Level),
            Index = stem.Artefacts.Index,
            Kinds = stem.Artefacts.Kinds.Select(kind => Enum.Parse<ArtefactKindDto>(kind.ToString())).ToList(),
            Reliability = Reliability(stem.Artefacts.Reliability),
        },
        Tips = tips.Take(ContractLimits.MaxTipsPerStem).ToList(),
    };

    private static QualitySummaryDto Count(IReadOnlyList<StemQuality> stems) => new()
    {
        Good = stems.Count(stem => stem.Rating == QualityRating.Good),
        Fair = stems.Count(stem => stem.Rating == QualityRating.Fair),
        Poor = stems.Count(stem => stem.Rating == QualityRating.Poor),
        NotPresent = stems.Count(stem => stem.Rating == QualityRating.NotPresent),
        Unknown = stems.Count(stem => stem.Rating == QualityRating.Unknown),
    };

    private static QualityReportStatusDto Status(QualityReportStatus status) => Enum.Parse<QualityReportStatusDto>(status.ToString());

    private static QualityLevelDto Level(QualityLevel level) => Enum.Parse<QualityLevelDto>(level.ToString());

    private static ReliabilityDto Reliability(Domain.Insights.Reliability reliability) => Enum.Parse<ReliabilityDto>(reliability.ToString());
}
