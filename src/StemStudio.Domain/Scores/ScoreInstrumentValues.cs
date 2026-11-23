using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Domain.Scores;

/// <summary>Tablature layout (feature 016, data-model 4.5).</summary>
public enum TabLayout
{
    /// <summary>Notation staff with a TAB staff below.</summary>
    NotationAndTab,

    /// <summary>TAB staff only.</summary>
    TabOnly,
}

/// <summary>Tablature request of a score (feature 016, FR-020).</summary>
/// <param name="Tuning">Tuning (must match the profile's instrument).</param>
/// <param name="Capo">Capo 0..12 (guitar tunings only).</param>
/// <param name="Layout">Layout.</param>
public sealed record TablatureSettings(TuningId Tuning, int Capo, TabLayout Layout);

/// <summary>Transposed part request of a score (feature 016, FR-050).</summary>
/// <param name="Instrument">Written instrument.</param>
/// <param name="Clef">Clef choice.</param>
public sealed record PartSettings(WrittenInstrument Instrument, ClefChoice Clef);

/// <summary>Tablature facts of a ready score (worker <c>TablatureSummary</c>).</summary>
public sealed record TablatureFacts
{
    /// <summary>Tuning.</summary>
    public required TuningId Tuning { get; init; }

    /// <summary>Strings (4..6).</summary>
    public required int Strings { get; init; }

    /// <summary>Capo.</summary>
    public required int Capo { get; init; }

    /// <summary>Layout.</summary>
    public required TabLayout Layout { get; init; }

    /// <summary>Notes moved by an octave to fit the neck.</summary>
    public int OctaveMoved { get; init; }

    /// <summary>Notes dropped from chords too wide for the hand.</summary>
    public int NotesDropped { get; init; }

    /// <summary>Wide stretches left in.</summary>
    public int WideStretches { get; init; }
}

/// <summary>Part facts of a ready score (worker <c>PartSummary</c>).</summary>
public sealed record PartFacts
{
    /// <summary>Written instrument.</summary>
    public required WrittenInstrument Instrument { get; init; }

    /// <summary>Part name.</summary>
    public required string Name { get; init; }

    /// <summary>Sounding -> written interval.</summary>
    public required string Interval { get; init; }

    /// <summary>Written key name, or null.</summary>
    public string? WrittenKey { get; init; }

    /// <summary>Clef used.</summary>
    public required ClefChoice Clef { get; init; }

    /// <summary>Notes outside the instrument's range.</summary>
    public int OutOfRange { get; init; }
}
