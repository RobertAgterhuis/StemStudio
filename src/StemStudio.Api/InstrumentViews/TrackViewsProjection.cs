using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.InstrumentViews;

/// <summary>
/// Adds the 1.15.0 fields to track DTOs (data-model 7.5): <c>instrumentViews</c> (effective statuses, so a track without a
/// drums split shows drum hits <c>unavailable</c>) and <c>key</c> (the 2.8 summary of ready chords, else absent).
/// </summary>
/// <param name="analyses">Analyses.</param>
/// <param name="quality">The quality badge (feature 020, <c>Track.quality</c>).</param>
public sealed class TrackViewsProjection(ITrackAnalysisRepository analyses, Insights.QualityView quality)
{
    /// <summary>The DTOs with their instrument view fields.</summary>
    /// <param name="items">Tracks with their DTOs.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The DTOs in the same order.</returns>
    public async Task<IReadOnlyList<TrackDto>> ApplyAsync(IReadOnlyList<(Track Track, TrackDto Dto)> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        var ids = items.Select(item => item.Track.Id).ToList();
        var statuses = (await analyses.StatusesAsync(ids, cancellationToken)).ToLookup(row => row.TrackId);
        var chords = await analyses.ReadyChordsAsync(ids, cancellationToken);
        var withViews = items.Select(item => (item.Track, item.Dto with
        {
            InstrumentViews = Statuses(item.Track, statuses[item.Track.Id].ToList()),
            Key = chords.TryGetValue(item.Track.Id, out var ready) ? KeySummary(ready) : null,
        })).ToList();
        return await quality.WithBadgesAsync(withViews, cancellationToken);
    }

    /// <summary>The key summary of a ready chord analysis (null without a key).</summary>
    /// <param name="analysis">The analysis.</param>
    /// <returns>The summary, or null.</returns>
    public static TrackKeySummaryDto? KeySummary(TrackAnalysis analysis) =>
        analysis is { Document: ChordChart { Key: { } key } chart }
            ? new TrackKeySummaryDto
            {
                Tonic = key.Tonic,
                Mode = InstrumentViewMapper.ToDto(key.Mode),
                Confidence = key.Confidence,
                Display = KeyFormatter.Display(key.Key, 0, key.Confidence),
                Regions = chart.KeyRegions.Count,
            }
            : null;

    private static InstrumentViewsStatusDto? Statuses(Track track, List<AnalysisStatusRow> rows)
    {
        var dto = new InstrumentViewsStatusDto
        {
            Chords = Effective(track, rows, AnalysisKind.Chords),
            DrumHits = Effective(track, rows, AnalysisKind.DrumHits),
            VocalPitch = Effective(track, rows, AnalysisKind.VocalPitch),
        };
        return dto == InstrumentViewsStatusDto.None ? null : dto;
    }

    /// <summary>The stored status, or unavailable when nothing is stored and a prerequisite is missing.</summary>
    private static AnalysisStatusDto Effective(Track track, List<AnalysisStatusRow> rows, AnalysisKind kind)
    {
        var stored = rows.Find(row => row.Kind == kind)?.Status ?? AnalysisStatus.None;
        var unavailable = stored == AnalysisStatus.None && AnalysisKindStrategies.UnavailableReason(AnalysisKindStrategies.Missing(kind, track)) is not null;
        return InstrumentViewMapper.ToDto(unavailable ? AnalysisStatus.Unavailable : stored);
    }
}
