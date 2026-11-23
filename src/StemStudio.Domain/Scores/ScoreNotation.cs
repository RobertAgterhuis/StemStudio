using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Domain.Scores;

/// <summary>A chord symbol placed above the top staff (worker <c>ScoreChordSymbol</c>): concert pitch, ms from the input start.</summary>
/// <param name="StartMs">Start relative to the transcription input.</param>
/// <param name="Root">Root 0..11.</param>
/// <param name="Quality">Quality.</param>
/// <param name="Bass">Bass pitch class, or null.</param>
public sealed record ScoreChord(int StartMs, int Root, ChordQuality Quality, int? Bass);

/// <summary>The chord symbols of a score and their spelling hint.</summary>
/// <param name="Symbols">Symbols in time order (at most 4,000).</param>
/// <param name="PreferFlats">Spelling hint of the chart's global key.</param>
public sealed record ScoreChords(IReadOnlyList<ScoreChord> Symbols, bool PreferFlats);

/// <summary>
/// Feature 016 score rules: which profiles take chord symbols (FR-017) and the symbols of a chord chart inside the
/// transcribed window (shifted to the input start; no-chord segments are not sent).
/// </summary>
public static class ScoreNotation
{
    /// <summary>Most chord symbols per score.</summary>
    public const int MaxChordSymbols = 4000;

    private static readonly HashSet<ScoreProfile> ChordProfiles =
        [ScoreProfile.Vocals, ScoreProfile.Guitar, ScoreProfile.Keys, ScoreProfile.Piano, ScoreProfile.Strings, ScoreProfile.Brass, ScoreProfile.Woodwinds];

    /// <summary>Message of a refused <c>includeChords</c>.</summary>
    public const string ChordsNotAllowed = "Chord symbols are placed only in vocals, guitar, keys, piano, strings, brass and woodwinds scores.";

    /// <summary>Whether a profile takes chord symbols.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>True for vocals, guitar, keys, piano, strings, brass and woodwinds.</returns>
    public static bool ChordsAllowed(ScoreProfile profile) => ChordProfiles.Contains(profile);

    /// <summary>The symbols of a chart inside the window <c>[offset, end)</c>, relative to the offset.</summary>
    /// <param name="chart">The chord chart (track time).</param>
    /// <param name="offsetSeconds">Seconds of the track before the input starts.</param>
    /// <param name="endSeconds">Track second where the input ends, or null for the track end.</param>
    /// <returns>The symbols with the chart's spelling hint.</returns>
    public static ScoreChords Chords(ChordChart chart, double offsetSeconds, double? endSeconds)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var start = (int)Math.Round(offsetSeconds * 1000);
        var end = endSeconds is { } seconds ? (int)Math.Round(seconds * 1000) : int.MaxValue;
        var symbols = chart.Segments
            .Where(segment => segment is { Root: not null, Quality: not null } && segment.EndMs > start && segment.StartMs < end)
            .Select(segment => new ScoreChord(Math.Max(segment.StartMs - start, 0), segment.Root!.Value, segment.Quality!.Value, segment.Bass))
            .Take(MaxChordSymbols)
            .ToList();
        return new ScoreChords(symbols, chart.PreferFlats);
    }
}
