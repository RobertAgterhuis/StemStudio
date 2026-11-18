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
public sealed class TrackViewsProjection(ITrackAnalysisRepository analyses)
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
        return items.Select(item => item.Dto with
        {
            InstrumentViews = Statuses(item.Track, statuses[item.Track.Id].ToList()),
            Key = chords.TryGetValue(item.Track.Id, out var ready) ? KeySummary(ready) : null,
        }).ToList();
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
        AnalysisStatusDto Of(AnalysisKind kind)
        {
            var stored = rows.Find(row => row.Kind == kind)?.Status ?? AnalysisStatus.None;
            var effective = stored == AnalysisStatus.None && AnalysisKindStrategies.UnavailableReason(AnalysisKindStrategies.Missing(kind, track)) is not null
                ? AnalysisStatus.Unavailable
                : stored;
            return InstrumentViewMapper.ToDto(effective);
        }

        var dto = new InstrumentViewsStatusDto { Chords = Of(AnalysisKind.Chords), DrumHits = Of(AnalysisKind.DrumHits), VocalPitch = Of(AnalysisKind.VocalPitch) };
        return dto == InstrumentViewsStatusDto.None ? null : dto;
    }
}
