namespace StemStudio.Contracts;

/// <summary>Limits of the instrument views that are part of the contract (backend-api 1.15.0, feature 016, data-model 7.6).</summary>
public static partial class ContractLimits
{
    /// <summary>Most chord segments per chart.</summary>
    public const int MaxChordSegments = 4000;

    /// <summary>Most key regions per chart.</summary>
    public const int MaxKeyRegions = 50;

    /// <summary>Most drum hits per map.</summary>
    public const int MaxDrumHits = 40000;

    /// <summary>Most 10 ms reference melody frames (15 minutes).</summary>
    public const int MaxPitchFrames = 90000;

    /// <summary>Highest centi-semitone value of a frame (MIDI 127).</summary>
    public const int MaxPitchCents = 12700;

    /// <summary>Longest chord label.</summary>
    public const int ChordLabelMaxLength = 32;

    /// <summary>Harte labels accepted by the chord edit (<c>EditChordRequest.label</c>).</summary>
    public const string HarteLabelPattern =
        "^(N|X|[A-G](#|b)?:(maj|min|dim|aug|min6|maj6|min7|minmaj7|maj7|7|dim7|hdim7|sus2|sus4)(/(b?[1-7]))?)$";

    /// <summary>Highest capo fret.</summary>
    public const int CapoMax = 12;

    /// <summary>Lowest display transpose (012).</summary>
    public const int MinDisplayTranspose = -12;

    /// <summary>Highest display transpose (012).</summary>
    public const int MaxDisplayTranspose = 12;

    /// <summary>Highest fret counted from the nut (<c>fret + capo</c>).</summary>
    public const int TabMaxFret = 24;

    /// <summary>Lowest singing tolerance in cents.</summary>
    public const int MinToleranceCents = 10;

    /// <summary>Highest singing tolerance in cents.</summary>
    public const int MaxToleranceCents = 200;

    /// <summary>Step of the singing tolerance slider in cents.</summary>
    public const int ToleranceStepCents = 5;

    /// <summary>Most rows of a chord sheet.</summary>
    public const int MaxChordSheetRows = 2000;

    /// <summary>Most chords in one chord sheet row.</summary>
    public const int MaxChordsPerSheetRow = 40;
}
