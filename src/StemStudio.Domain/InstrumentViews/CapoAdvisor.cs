namespace StemStudio.Domain.InstrumentViews;

/// <summary>A chord of a progression with its length (input of <see cref="CapoAdvisor"/>).</summary>
/// <param name="Root">Root 0..11.</param>
/// <param name="Quality">Quality.</param>
/// <param name="DurationMs">Length.</param>
public sealed record TimedChord(int Root, ChordQuality Quality, int DurationMs);

/// <summary>One suggested capo position.</summary>
/// <param name="Capo">Capo 0..7.</param>
/// <param name="ShapeKey">The key the shapes are played in ("C major").</param>
/// <param name="OpenShare">Share of the chord time with an open shape, 0..1 (4 decimals).</param>
/// <param name="FewOpenShapes">The best suggestion has fewer than half open shapes.</param>
public sealed record CapoSuggestion(int Capo, string ShapeKey, double OpenShare, bool FewOpenShapes);

/// <summary>Capo advice for a transpose.</summary>
/// <param name="Shortcut">Capo that keeps the original shapes, or null.</param>
/// <param name="Suggestions">At most three suggestions, best first.</param>
public sealed record CapoAdvice(int? Shortcut, IReadOnlyList<CapoSuggestion> Suggestions);

/// <summary>
/// The capo advisor of research R-14 (scope addition S12): for capo 0..7 the share of chord time with an open shape,
/// ranked by <c>open - 0.02 x capo</c> (exact ties by the lower capo), top three; the transpose shortcut keeps the original
/// shapes.
/// </summary>
public static class CapoAdvisor
{
    /// <summary>Highest suggested capo.</summary>
    public const int MaxSuggestedCapo = 7;

    /// <summary>Number of suggestions.</summary>
    public const int SuggestionCount = 3;

    /// <summary>Penalty per fret.</summary>
    public const double FretPenalty = 0.02;

    /// <summary>Best open share below which the advice says "few open shapes".</summary>
    public const double FewOpenBelow = 0.5;

    private static readonly Dictionary<ChordQuality, HashSet<int>> OpenShapes = new()
    {
        [ChordQuality.Maj] = [0, 9, 7, 4, 2],
        [ChordQuality.Min] = [9, 4, 2],
        [ChordQuality.Seventh] = [0, 9, 7, 4, 2, 11],
        [ChordQuality.Min7] = [9, 4, 2],
        [ChordQuality.Maj7] = [0, 9, 7, 4, 2, 5],
        [ChordQuality.Sus2] = [9, 2],
        [ChordQuality.Sus4] = [9, 2, 4],
    };

    /// <summary>The shape of a chord with a capo is an open shape (R-14 table; the bass is ignored).</summary>
    /// <param name="chord">The chord at concert pitch.</param>
    /// <param name="display">Transpose and capo.</param>
    /// <returns>True for an open shape.</returns>
    public static bool IsOpenShape(TimedChord chord, ChordDisplay display)
    {
        ArgumentNullException.ThrowIfNull(chord);
        return OpenShapes.TryGetValue(chord.Quality, out var roots) && roots.Contains(PitchClass.Of(chord.Root + display.Shift));
    }

    /// <summary>The transpose shortcut (FR-077): capo t for t in 1..7, capo 12 + t for t in -5..-1 when at most 7.</summary>
    /// <param name="transpose">The 012 transpose.</param>
    /// <returns>The capo, or null.</returns>
    public static int? Shortcut(int transpose) => transpose switch
    {
        >= 1 and <= MaxSuggestedCapo => transpose,
        >= -5 and <= -1 when 12 + transpose <= MaxSuggestedCapo => 12 + transpose,
        _ => null,
    };

    /// <summary>The advice for a progression.</summary>
    /// <param name="chords">The chords (no-chord segments left out).</param>
    /// <param name="key">The global key at concert pitch.</param>
    /// <param name="transpose">The 012 transpose.</param>
    /// <returns>Shortcut and suggestions.</returns>
    public static CapoAdvice Advise(IReadOnlyList<TimedChord> chords, MusicalKey key, int transpose)
    {
        ArgumentNullException.ThrowIfNull(chords);
        var total = chords.Sum(chord => (double)chord.DurationMs);
        var ranked = Enumerable.Range(0, MaxSuggestedCapo + 1)
            .Select(capo => (Capo: capo, Share: OpenShare(chords, total, new ChordDisplay(transpose, capo))))
            .OrderByDescending(entry => entry.Share - (FretPenalty * entry.Capo))
            .ThenBy(entry => entry.Capo)
            .Take(SuggestionCount)
            .ToList();
        var few = ranked[0].Share < FewOpenBelow;
        var suggestions = ranked
            .Select(entry => new CapoSuggestion(entry.Capo, KeyFormatter.Name(ChordSpeller.Move(key, new ChordDisplay(transpose, entry.Capo))!.Value), entry.Share, few))
            .ToList();
        return new CapoAdvice(Shortcut(transpose), suggestions);
    }

    private static double OpenShare(IReadOnlyList<TimedChord> chords, double total, ChordDisplay display) =>
        total <= 0 ? 0 : Math.Round(chords.Where(chord => IsOpenShape(chord, display)).Sum(chord => (double)chord.DurationMs) / total, 4);
}
