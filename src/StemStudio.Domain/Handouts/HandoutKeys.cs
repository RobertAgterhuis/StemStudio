using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Domain.Handouts;

/// <summary>
/// The song key a handout prints, in the written key of its transposition (data-model 2.5 <c>SongFacts.key</c>): the chord
/// chart's key, else the key of the first concert-pitch score (the family's notation first, then the song's other scores).
/// </summary>
public static class HandoutKeys
{
    private static readonly Dictionary<char, int> Letters = new() { ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5, ['G'] = 7, ['A'] = 9, ['B'] = 11 };

    /// <summary>The key text and its source (<c>chords</c> or <c>score</c>), or nulls without a key.</summary>
    /// <param name="chart">The chord chart, or null.</param>
    /// <param name="scores">The scores in order of preference.</param>
    /// <param name="transposition">The handout's transposition.</param>
    /// <returns>Key text ("B minor") and source.</returns>
    public static (string? Key, string? Source) Of(ChordChart? chart, IEnumerable<NotationItem> scores, Transposition transposition)
    {
        ArgumentNullException.ThrowIfNull(scores);
        if (HandoutChords.Key(chart, transposition) is { } chordKey)
        {
            return (chordKey, "chords");
        }

        var scoreKey = scores.Where(IsConcertPitch).Select(item => Parse(item.Key)).FirstOrDefault(key => key is not null);
        return scoreKey is { } key ? (KeyFormatter.Name(ChordSpeller.Move(key, HandoutChords.Display(transposition))!.Value), "score") : (null, null);
    }

    /// <summary>Whether an item is written at concert pitch (a score or tab, not a transposed part or drums).</summary>
    /// <param name="item">The item.</param>
    /// <returns>True for concert pitch.</returns>
    public static bool IsConcertPitch(NotationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind is NotationKind.Score or NotationKind.Tab && (item.Transposition is null or Transposition.C);
    }

    /// <summary>A score key in music21 spelling (<c>A</c>, <c>F#</c>, <c>B-</c> = B-flat), or null when it cannot be read.</summary>
    /// <param name="key">The key, or null.</param>
    /// <returns>The key, or null.</returns>
    public static MusicalKey? Parse(KeyInfo? key)
    {
        if (key?.Tonic is not { Length: >= 1 and <= 4 } tonic || !Letters.TryGetValue(tonic[0], out var letter))
        {
            return null;
        }

        var accidentals = tonic[1..];
        var sharps = accidentals.Count(sign => sign == '#');
        var flats = accidentals.Count(sign => sign == '-');
        return sharps + flats == accidentals.Length && (sharps == 0 || flats == 0) ? new MusicalKey(PitchClass.Of(letter + sharps - flats), key.Mode) : null;
    }
}
