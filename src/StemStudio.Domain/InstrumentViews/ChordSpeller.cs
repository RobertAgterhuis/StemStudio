using StemStudio.Domain.Scores;

namespace StemStudio.Domain.InstrumentViews;

/// <summary>A key by tonic pitch class (C = 0) and mode.</summary>
/// <param name="Tonic">Tonic 0..11.</param>
/// <param name="Mode">Major or minor.</param>
public readonly record struct MusicalKey(int Tonic, KeyMode Mode);

/// <summary>A chord by pitch classes; a null root is "no chord".</summary>
/// <param name="Root">Root 0..11, or null for <c>N</c>/<c>X</c>.</param>
/// <param name="Quality">Quality, or null with a null root.</param>
/// <param name="Bass">Bass pitch class when it is not the root, or null.</param>
public sealed record ChordName(int? Root, ChordQuality? Quality, int? Bass)
{
    /// <summary>No chord.</summary>
    public static ChordName None { get; } = new(null, null, null);
}

/// <summary>How a chord is displayed: the 012 transpose and the capo (data-model 2.5).</summary>
/// <param name="Transpose">Transpose -12..12.</param>
/// <param name="Capo">Capo 0..12 (guitar family; 0 otherwise).</param>
public readonly record struct ChordDisplay(int Transpose, int Capo)
{
    /// <summary>The sounding display (no transpose, no capo).</summary>
    public static ChordDisplay Sounding { get; } = new(0, 0);

    /// <summary>The shift in semitones applied to every pitch class.</summary>
    public int Shift => Transpose - Capo;
}

/// <summary>Pitch-class helpers.</summary>
public static class PitchClass
{
    /// <summary>Wraps any integer into 0..11.</summary>
    /// <param name="value">A semitone value.</param>
    /// <returns>The pitch class.</returns>
    public static int Of(int value) => ((value % 12) + 12) % 12;
}

/// <summary>
/// Chord and note spelling of the instrument views (data-model 2.5, shared vectors 1.0.0): sharps unless the moved key's
/// relative major is F, Bb, Eb, Ab, Db or Gb; no key means sharps; no chord is "N.C.".
/// </summary>
public static class ChordSpeller
{
    /// <summary>The symbol of "no chord".</summary>
    public const string NoChord = "N.C.";

    private static readonly string[] Sharps = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly string[] Flats = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];
    private static readonly HashSet<int> FlatMajorTonics = [5, 10, 3, 8, 1, 6];

    /// <summary>The flat-key rule: flats when the relative major tonic is one of F Bb Eb Ab Db Gb.</summary>
    /// <param name="key">The key (already moved).</param>
    /// <returns>True for flats.</returns>
    public static bool UsesFlats(MusicalKey key) =>
        FlatMajorTonics.Contains(key.Mode == KeyMode.Major ? PitchClass.Of(key.Tonic) : PitchClass.Of(key.Tonic + 3));

    /// <summary>The name of a pitch class.</summary>
    /// <param name="pitchClass">Any semitone value (wrapped).</param>
    /// <param name="flats">Flat names.</param>
    /// <returns>The name.</returns>
    public static string NoteName(int pitchClass, bool flats) => (flats ? Flats : Sharps)[PitchClass.Of(pitchClass)];

    /// <summary>The key moved by the display shift.</summary>
    /// <param name="key">The concert key, or null.</param>
    /// <param name="display">Transpose and capo.</param>
    /// <returns>The moved key, or null.</returns>
    public static MusicalKey? Move(MusicalKey? key, ChordDisplay display) =>
        key is { } k ? new MusicalKey(PitchClass.Of(k.Tonic + display.Shift), k.Mode) : null;

    /// <summary>The display symbol of a chord ("Am7/G", "Eb", "N.C.").</summary>
    /// <param name="chord">The chord at concert pitch.</param>
    /// <param name="key">The spelling key at concert pitch (the region key, else the global key), or null for sharps.</param>
    /// <param name="display">Transpose and capo.</param>
    /// <returns>The symbol.</returns>
    public static string Symbol(ChordName chord, MusicalKey? key, ChordDisplay display)
    {
        ArgumentNullException.ThrowIfNull(chord);
        if (chord is not { Root: { } root, Quality: { } quality })
        {
            return NoChord;
        }

        var flats = Move(key, display) is { } moved && UsesFlats(moved);
        var text = NoteName(root + display.Shift, flats) + ChordQualities.Suffix(quality);
        return chord.Bass is { } bass && bass != root ? $"{text}/{NoteName(bass + display.Shift, flats)}" : text;
    }
}
