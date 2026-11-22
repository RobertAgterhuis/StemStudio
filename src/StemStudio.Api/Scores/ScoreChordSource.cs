using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Scores;

/// <summary>
/// The chord symbols of a score with <c>includeChords</c> (feature 016 FR-017): the source track's ready (or stale) chord chart
/// inside the transcribed window, shifted to the input start; <c>409 chords-not-ready</c> otherwise.
/// </summary>
/// <param name="analyses">Analyses.</param>
public sealed class ScoreChordSource(ITrackAnalysisRepository analyses)
{
    /// <summary>The chart that a score of this track could use, or null.</summary>
    /// <param name="trackId">The source track, or null.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The chart, or null when the chords are not ready.</returns>
    public async Task<ChordChart?> ChartAsync(Guid? trackId, CancellationToken cancellationToken) =>
        trackId is { } id && await analyses.FindAsync(id, AnalysisKind.Chords, cancellationToken) is { Status: AnalysisStatus.Ready or AnalysisStatus.Stale, Document: ChordChart chart }
            ? chart
            : null;

    /// <summary>The draft with its chord symbols, or the 409 problem.</summary>
    /// <param name="draft">The planned draft.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The draft (unchanged without <c>includeChords</c>) or the problem.</returns>
    public async Task<(ScoreDraft? Draft, IResult? Problem)> AttachAsync(ScoreDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.Recipe.IncludeChords)
        {
            return (draft, null);
        }

        if (await ChartAsync(draft.Source.Track?.Id, cancellationToken) is not { } chart)
        {
            return (null, ScoreProblems.ChordsNotReady());
        }

        return (WithChords(draft, chart), null);
    }

    /// <summary>The draft with the chord symbols of its window in the track (asset range plus request range).</summary>
    private static ScoreDraft WithChords(ScoreDraft draft, ChordChart chart)
    {
        var offset = TempoGridResolver.Offset(draft.Source.AssetRangeStart, draft.Recipe.Range?.StartSeconds);
        var end = (draft.Source.AssetRangeStart ?? 0) + (draft.Recipe.Range?.EndSeconds ?? draft.Source.DurationSeconds);
        return draft with { Chords = ScoreNotation.Chords(chart, offset, end) };
    }
}
