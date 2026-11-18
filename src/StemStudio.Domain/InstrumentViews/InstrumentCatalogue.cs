namespace StemStudio.Domain.InstrumentViews;

/// <summary>Tablature tunings (data-model 4.1).</summary>
public enum TuningId
{
    /// <summary>E A D G B E.</summary>
    GuitarStandard,

    /// <summary>Drop D.</summary>
    GuitarDropD,

    /// <summary>Half step down (Eb).</summary>
    GuitarHalfStepDown,

    /// <summary>Full step down (D).</summary>
    GuitarFullStepDown,

    /// <summary>DADGAD.</summary>
    GuitarDadgad,

    /// <summary>Open G.</summary>
    GuitarOpenG,

    /// <summary>Open D.</summary>
    GuitarOpenD,

    /// <summary>4-string E A D G.</summary>
    Bass4Standard,

    /// <summary>4-string drop D.</summary>
    Bass4DropD,

    /// <summary>4-string half step down.</summary>
    Bass4HalfStepDown,

    /// <summary>5-string B E A D G.</summary>
    Bass5Standard,

    /// <summary>6-string B E A D G C.</summary>
    Bass6Standard,
}

/// <summary>Written instruments of the transposed parts (data-model 4.2).</summary>
public enum WrittenInstrument
{
    /// <summary>Concert pitch (the profile's own clef and range).</summary>
    Concert,

    /// <summary>Trumpet in B-flat.</summary>
    TrumpetBb,

    /// <summary>Clarinet in B-flat.</summary>
    ClarinetBb,

    /// <summary>Soprano saxophone in B-flat.</summary>
    SopranoSaxBb,

    /// <summary>Tenor saxophone in B-flat.</summary>
    TenorSaxBb,

    /// <summary>Bass clarinet in B-flat.</summary>
    BassClarinetBb,

    /// <summary>Alto saxophone in E-flat.</summary>
    AltoSaxEb,

    /// <summary>Baritone saxophone in E-flat.</summary>
    BaritoneSaxEb,

    /// <summary>Clarinet in E-flat.</summary>
    ClarinetEb,

    /// <summary>Horn in F.</summary>
    HornF,

    /// <summary>English horn in F.</summary>
    EnglishHornF,

    /// <summary>Flute.</summary>
    Flute,

    /// <summary>Oboe.</summary>
    Oboe,

    /// <summary>Violin.</summary>
    Violin,

    /// <summary>Viola.</summary>
    Viola,

    /// <summary>Cello.</summary>
    Cello,

    /// <summary>Double bass.</summary>
    DoubleBass,

    /// <summary>Trombone.</summary>
    Trombone,

    /// <summary>Bassoon.</summary>
    Bassoon,

    /// <summary>Tuba.</summary>
    Tuba,
}

/// <summary>Clef choices of a part (data-model 4.3).</summary>
public enum ClefChoice
{
    /// <summary>The instrument's default clef, else the profile's.</summary>
    Auto,

    /// <summary>Treble.</summary>
    Treble,

    /// <summary>Bass.</summary>
    Bass,

    /// <summary>Alto.</summary>
    Alto,

    /// <summary>Tenor.</summary>
    Tenor,

    /// <summary>Treble sounding an octave lower.</summary>
    Treble8vb,

    /// <summary>Bass sounding an octave lower.</summary>
    Bass8vb,
}

/// <summary>A tuning: strings low to high as sounding MIDI pitches.</summary>
/// <param name="Id">Id.</param>
/// <param name="Instrument"><c>guitar</c> or <c>bass</c>.</param>
/// <param name="Label">Label.</param>
/// <param name="Pitches">Sounding MIDI pitches, low to high.</param>
public sealed record Tuning(TuningId Id, string Instrument, string Label, IReadOnlyList<int> Pitches)
{
    /// <summary>True for a guitar tuning (capo allowed).</summary>
    public bool IsGuitar => Instrument == InstrumentCatalogue.GuitarInstrument;
}

/// <summary>A written instrument (data-model 4.2).</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Part name ("" for concert = the profile's instrument name).</param>
/// <param name="KeyName"><c>C</c>, <c>Bb</c>, <c>Eb</c> or <c>F</c>.</param>
/// <param name="Interval">Sounding -> written interval (music21 notation).</param>
/// <param name="Range">Default clef and sounding range.</param>
public sealed record WrittenInstrumentInfo(WrittenInstrument Id, string Name, string KeyName, string Interval, InstrumentRange Range)
{
    /// <summary>The written interval in semitones (written = sounding + semitones).</summary>
    public int Semitones => InstrumentCatalogue.IntervalSemitones(Interval);
}

/// <summary>Default clef and sounding range of a written instrument.</summary>
/// <param name="DefaultClef">Clef (<see cref="ClefChoice.Auto"/> = the profile's clef).</param>
/// <param name="LowMidi">Lowest sounding MIDI pitch.</param>
/// <param name="HighMidi">Highest sounding MIDI pitch.</param>
public sealed record InstrumentRange(ClefChoice DefaultClef, int LowMidi, int HighMidi);

/// <summary>The static instrument catalogue: tunings, written instruments and clefs (data-model 4.1-4.3).</summary>
public static class InstrumentCatalogue
{
    /// <summary>Instrument of the guitar tunings.</summary>
    public const string GuitarInstrument = "guitar";

    /// <summary>Instrument of the bass tunings.</summary>
    public const string BassInstrument = "bass";

    private static readonly Dictionary<string, int> Intervals = new(StringComparer.Ordinal)
    {
        ["P1"] = 0, ["M2"] = 2, ["M9"] = 14, ["M6"] = 9, ["M13"] = 21, ["-m3"] = -3, ["P5"] = 7, ["P8"] = 12,
    };

    /// <summary>The tunings in catalogue order.</summary>
    public static IReadOnlyList<Tuning> Tunings { get; } =
    [
        Guitar(TuningId.GuitarStandard, "Standard (E A D G B E)", 40, 45, 50, 55, 59, 64),
        Guitar(TuningId.GuitarDropD, "Drop D", 38, 45, 50, 55, 59, 64),
        Guitar(TuningId.GuitarHalfStepDown, "Half step down (Eb)", 39, 44, 49, 54, 58, 63),
        Guitar(TuningId.GuitarFullStepDown, "Full step down (D)", 38, 43, 48, 53, 57, 62),
        Guitar(TuningId.GuitarDadgad, "DADGAD", 38, 45, 50, 55, 57, 62),
        Guitar(TuningId.GuitarOpenG, "Open G", 38, 43, 50, 55, 59, 62),
        Guitar(TuningId.GuitarOpenD, "Open D", 38, 45, 50, 54, 57, 62),
        Bass(TuningId.Bass4Standard, "4-string (E A D G)", 28, 33, 38, 43),
        Bass(TuningId.Bass4DropD, "4-string drop D", 26, 33, 38, 43),
        Bass(TuningId.Bass4HalfStepDown, "4-string half step down", 27, 32, 37, 42),
        Bass(TuningId.Bass5Standard, "5-string (B E A D G)", 23, 28, 33, 38, 43),
        Bass(TuningId.Bass6Standard, "6-string (B E A D G C)", 23, 28, 33, 38, 43, 48),
    ];

    /// <summary>The written instruments in catalogue order.</summary>
    public static IReadOnlyList<WrittenInstrumentInfo> Instruments { get; } =
    [
        new(WrittenInstrument.Concert, string.Empty, "C", "P1", new(ClefChoice.Auto, 0, 127)),
        new(WrittenInstrument.TrumpetBb, "Trumpet in B-flat", "Bb", "M2", new(ClefChoice.Treble, 52, 82)),
        new(WrittenInstrument.ClarinetBb, "Clarinet in B-flat", "Bb", "M2", new(ClefChoice.Treble, 50, 91)),
        new(WrittenInstrument.SopranoSaxBb, "Soprano Saxophone in B-flat", "Bb", "M2", new(ClefChoice.Treble, 56, 87)),
        new(WrittenInstrument.TenorSaxBb, "Tenor Saxophone in B-flat", "Bb", "M9", new(ClefChoice.Treble, 44, 75)),
        new(WrittenInstrument.BassClarinetBb, "Bass Clarinet in B-flat", "Bb", "M9", new(ClefChoice.Treble, 38, 77)),
        new(WrittenInstrument.AltoSaxEb, "Alto Saxophone in E-flat", "Eb", "M6", new(ClefChoice.Treble, 49, 80)),
        new(WrittenInstrument.BaritoneSaxEb, "Baritone Saxophone in E-flat", "Eb", "M13", new(ClefChoice.Treble, 36, 68)),
        new(WrittenInstrument.ClarinetEb, "Clarinet in E-flat", "Eb", "-m3", new(ClefChoice.Treble, 53, 91)),
        new(WrittenInstrument.HornF, "Horn in F", "F", "P5", new(ClefChoice.Treble, 34, 77)),
        new(WrittenInstrument.EnglishHornF, "English Horn in F", "F", "P5", new(ClefChoice.Treble, 52, 81)),
        new(WrittenInstrument.Flute, "Flute", "C", "P1", new(ClefChoice.Treble, 60, 96)),
        new(WrittenInstrument.Oboe, "Oboe", "C", "P1", new(ClefChoice.Treble, 58, 91)),
        new(WrittenInstrument.Violin, "Violin", "C", "P1", new(ClefChoice.Treble, 55, 100)),
        new(WrittenInstrument.Viola, "Viola", "C", "P1", new(ClefChoice.Alto, 48, 88)),
        new(WrittenInstrument.Cello, "Cello", "C", "P1", new(ClefChoice.Bass, 36, 76)),
        new(WrittenInstrument.DoubleBass, "Double Bass", "C", "P8", new(ClefChoice.Bass, 28, 67)),
        new(WrittenInstrument.Trombone, "Trombone", "C", "P1", new(ClefChoice.Bass, 40, 72)),
        new(WrittenInstrument.Bassoon, "Bassoon", "C", "P1", new(ClefChoice.Bass, 34, 75)),
        new(WrittenInstrument.Tuba, "Tuba", "C", "P1", new(ClefChoice.Bass, 28, 65)),
    ];

    /// <summary>The clef choices in catalogue order.</summary>
    public static IReadOnlyList<ClefChoice> Clefs { get; } = Enum.GetValues<ClefChoice>();

    /// <summary>The tuning of an id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The tuning.</returns>
    public static Tuning TuningOf(TuningId id) => Tunings[(int)id];

    /// <summary>The written instrument of an id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The instrument.</returns>
    public static WrittenInstrumentInfo InstrumentOf(WrittenInstrument id) => Instruments[(int)id];

    /// <summary>Semitones of a music21 interval name of the catalogue.</summary>
    /// <param name="interval"><c>P1</c>, <c>M2</c>, <c>M9</c>, <c>M6</c>, <c>M13</c>, <c>-m3</c>, <c>P5</c> or <c>P8</c>.</param>
    /// <returns>Semitones (negative = written lower).</returns>
    /// <exception cref="ArgumentException">Not an interval of the catalogue.</exception>
    public static int IntervalSemitones(string interval) =>
        Intervals.TryGetValue(interval, out var semitones) ? semitones : throw new ArgumentException($"'{interval}' is not a catalogue interval.", nameof(interval));

    private static Tuning Guitar(TuningId id, string label, params int[] pitches) => new(id, GuitarInstrument, label, pitches);

    private static Tuning Bass(TuningId id, string label, params int[] pitches) => new(id, BassInstrument, label, pitches);
}
