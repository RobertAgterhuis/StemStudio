using System.Globalization;
using StemStudio.Domain.Scores;

namespace StemStudio.Domain.InstrumentViews;

/// <summary>Key display of the instrument views (data-model 2.8, shared vectors 1.0.0).</summary>
public static class KeyFormatter
{
    /// <summary>Confidence below which a key is shown as uncertain.</summary>
    public const double UncertainBelow = 0.5;

    /// <summary>"&lt;Tonic&gt; major|minor", spelled by the chord rule ("Eb major", "F# minor").</summary>
    /// <param name="key">The key.</param>
    /// <returns>The name.</returns>
    public static string Name(MusicalKey key) =>
        $"{ChordSpeller.NoteName(key.Tonic, ChordSpeller.UsesFlats(key))} {(key.Mode == KeyMode.Major ? "major" : "minor")}";

    /// <summary>The key as shown: transposed with the original, "(uncertain)" below 0.5 confidence.</summary>
    /// <param name="key">The concert key.</param>
    /// <param name="transpose">The 012 transpose, -12..12.</param>
    /// <param name="confidence">Confidence 0..1.</param>
    /// <returns>"D major (original C major, +2)" or "A minor (uncertain)".</returns>
    public static string Display(MusicalKey key, int transpose, double confidence)
    {
        var original = Name(key);
        var text = transpose == 0
            ? original
            : string.Create(CultureInfo.InvariantCulture, $"{Name(key with { Tonic = PitchClass.Of(key.Tonic + transpose) })} (original {original}, {transpose:+0;-0})");
        return confidence < UncertainBelow ? text + " (uncertain)" : text;
    }

    /// <summary>The written key of a transposing instrument: concert tonic + the instrument's semitones, spelled the same way.</summary>
    /// <param name="key">The concert key.</param>
    /// <param name="instrument">The written instrument.</param>
    /// <returns>The written key name, or null for <see cref="WrittenInstrument.Concert"/>.</returns>
    public static string? WrittenKey(MusicalKey key, WrittenInstrument instrument) =>
        instrument == WrittenInstrument.Concert
            ? null
            : Name(key with { Tonic = PitchClass.Of(key.Tonic + InstrumentCatalogue.InstrumentOf(instrument).Semitones) });
}
