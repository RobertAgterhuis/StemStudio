#pragma warning disable IDE0005, CS9113, CS1998, IDE0060, CA1822, CA1801, IDE0051, IDE0052, CS0169, CS0414
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
    public Task<QualityReportDto> ReportAsync(Track track, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>Adds the badge (<c>Track.quality</c>) to track DTOs.</summary>
    /// <param name="items">Tracks with their DTOs.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The DTOs with the badge.</returns>
    public Task<IReadOnlyList<TrackDto>> WithBadgesAsync(IReadOnlyList<(Track Track, TrackDto Dto)> items, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// True when the stems shown were measured with another analyzer version than the worker's current one, or the stem files
    /// changed since (a report without measured stems is never outdated).
    /// </summary>
    /// <param name="track">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The flag.</returns>
    public Task<bool> OutdatedAsync(Track track, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
