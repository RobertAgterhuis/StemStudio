using StemStudio.Contracts.Insights;

namespace StemStudio.Web.Insights;

/// <summary>One row of the quality card: a stem with the detail stems (and <c>rest</c>) shown under it (US1 AS2).</summary>
/// <param name="Stem">The stem.</param>
/// <param name="Children">Its kept detail stems in report order; empty for stems without a split.</param>
public sealed record QualityGroup(StemQualityViewDto Stem, IReadOnlyList<StemQualityViewDto> Children);

/// <summary>Order and state rules of the stem quality report (feature 020, data-model 7).</summary>
public static class QualityLayout
{
    /// <summary>Groups the stems: every standard stem in report order with its children; a child whose parent is not
    /// in the report becomes its own row at the end.</summary>
    /// <param name="stems">The report stems.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<QualityGroup> Group(IReadOnlyList<StemQualityViewDto> stems)
    {
        var parents = stems.Where(s => s.Parent is null).Select(s => s.Channel).ToHashSet(StringComparer.Ordinal);
        var rows = stems.Where(s => s.Parent is null)
            .Select(s => new QualityGroup(s, [.. stems.Where(c => c.Parent == s.Channel)]));
        var orphans = stems.Where(s => s.Parent is { } parent && !parents.Contains(parent)).Select(s => new QualityGroup(s, []));
        return [.. rows, .. orphans];
    }

    /// <summary>True while a report is queued or running (poll; the check button is disabled).</summary>
    /// <param name="status">The status.</param>
    /// <returns>Whether the report is in progress.</returns>
    public static bool IsActive(QualityReportStatusDto status) => status is QualityReportStatusDto.Queued or QualityReportStatusDto.Running;

    /// <summary>The check button text: "Check stem quality" before the first report, "Check again" after.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The text.</returns>
    public static string CheckText(QualityReportStatusDto status) => status == QualityReportStatusDto.None ? "Check stem quality" : "Check again";

    /// <summary>Finds the view of a channel.</summary>
    /// <param name="report">The report; null when not loaded.</param>
    /// <param name="channel">ChannelId.</param>
    /// <returns>The view or null.</returns>
    public static StemQualityViewDto? Find(QualityReportDto? report, string channel) =>
        report?.Stems.FirstOrDefault(s => string.Equals(s.Channel, channel, StringComparison.Ordinal));
}
