using StemStudio.Contracts.Insights;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Insights;

/// <summary>
/// Builds the quality report view of a track from its stored report and current state (FR-001, FR-005): the derived
/// <c>outdated</c> flag (analyzer version or stem files changed) and the tips per stem.
/// </summary>
/// <param name="inputs">Stem files of the track.</param>
/// <param name="versions">The worker's current analyzer version.</param>
/// <param name="tips">Builds the tips from the current track state.</param>
public sealed class QualityView(QualityInputs inputs, AnalyzerVersionCache versions, QualityTips tips)
{
    /// <summary>The report of a track.</summary>
    /// <param name="track">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The DTO.</returns>
    public async Task<QualityReportDto> ReportAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        var outdated = await OutdatedAsync(track, cancellationToken);
        return QualityMapper.ToDto(track.QualityReport, outdated, track.QualityReport is null ? new Dictionary<string, IReadOnlyList<TipDto>>() : tips.For(track));
    }

    /// <summary>Adds the badge (<c>Track.quality</c>) to track DTOs.</summary>
    /// <param name="items">Tracks with their DTOs.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The DTOs with the badge.</returns>
    public async Task<IReadOnlyList<TrackDto>> WithBadgesAsync(IReadOnlyList<(Track Track, TrackDto Dto)> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        var result = new List<TrackDto>(items.Count);
        foreach (var (track, dto) in items)
        {
            result.Add(dto with { Quality = QualityMapper.Badge(track.QualityReport, await OutdatedAsync(track, cancellationToken)) });
        }

        return result;
    }

    /// <summary>
    /// True when the stems shown were measured with another analyzer version than the worker's current one, or the stem files
    /// changed since (a report without measured stems is never outdated).
    /// </summary>
    /// <param name="track">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The flag.</returns>
    public async Task<bool> OutdatedAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (track.QualityReport is not { Stems.Count: > 0 } report)
        {
            return false;
        }

        var current = await versions.CurrentAsync(cancellationToken);
        return (current is not null && !string.Equals(current, report.AnalyzerVersion, StringComparison.Ordinal))
            || !string.Equals(inputs.CurrentVersion(track), report.StemsVersion, StringComparison.Ordinal);
    }
}
