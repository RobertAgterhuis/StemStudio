namespace StemStudio.Domain.InstrumentViews;

/// <summary>Chord quality of the <c>full</c> vocabulary (data-model 2.1); <c>simple</c> uses only major and minor.</summary>
public enum ChordQuality
{
    /// <summary><c>maj</c>, shown as "".</summary>
    Maj,

    /// <summary><c>min</c>, shown as "m".</summary>
    Min,

    /// <summary><c>dim</c>.</summary>
    Dim,

    /// <summary><c>aug</c>.</summary>
    Aug,

    /// <summary><c>min6</c>, shown as "m6".</summary>
    Min6,

    /// <summary><c>maj6</c>, shown as "6".</summary>
    Maj6,

    /// <summary><c>min7</c>, shown as "m7".</summary>
    Min7,

    /// <summary><c>minmaj7</c>, shown as "m(maj7)".</summary>
    MinMaj7,

    /// <summary><c>maj7</c>.</summary>
    Maj7,

    /// <summary><c>7</c> (dominant seventh).</summary>
    Seventh,

    /// <summary><c>dim7</c>.</summary>
    Dim7,

    /// <summary><c>hdim7</c>, shown as "m7b5".</summary>
    HalfDim7,

    /// <summary><c>sus2</c>.</summary>
    Sus2,

    /// <summary><c>sus4</c>.</summary>
    Sus4,
}

/// <summary>Harte names and display suffixes of the chord qualities (data-model 2.1).</summary>
public static class ChordQualities
{
    private static readonly (ChordQuality Quality, string Harte, string Suffix)[] Table =
    [
        (ChordQuality.Maj, "maj", string.Empty), (ChordQuality.Min, "min", "m"), (ChordQuality.Dim, "dim", "dim"),
        (ChordQuality.Aug, "aug", "aug"), (ChordQuality.Min6, "min6", "m6"), (ChordQuality.Maj6, "maj6", "6"),
        (ChordQuality.Min7, "min7", "m7"), (ChordQuality.MinMaj7, "minmaj7", "m(maj7)"), (ChordQuality.Maj7, "maj7", "maj7"),
        (ChordQuality.Seventh, "7", "7"), (ChordQuality.Dim7, "dim7", "dim7"), (ChordQuality.HalfDim7, "hdim7", "m7b5"),
        (ChordQuality.Sus2, "sus2", "sus2"), (ChordQuality.Sus4, "sus4", "sus4"),
    ];

    /// <summary>The Harte name (<c>maj</c>, <c>min7</c>, <c>7</c>, ...).</summary>
    /// <param name="quality">The quality.</param>
    /// <returns>The name.</returns>
    public static string HarteName(ChordQuality quality) => Row(quality).Harte;

    /// <summary>The display suffix ("" for major, "m" for minor, ...).</summary>
    /// <param name="quality">The quality.</param>
    /// <returns>The suffix.</returns>
    public static string Suffix(ChordQuality quality) => Row(quality).Suffix;

    /// <summary>Reads a Harte quality name.</summary>
    /// <param name="harte">The name.</param>
    /// <returns>The quality, or null when unknown.</returns>
    public static ChordQuality? FromHarte(string harte) =>
        Array.Find(Table, row => string.Equals(row.Harte, harte, StringComparison.Ordinal)) is { Harte: not null } row ? row.Quality : null;

    private static (ChordQuality Quality, string Harte, string Suffix) Row(ChordQuality quality) => Table[(int)quality];
}
