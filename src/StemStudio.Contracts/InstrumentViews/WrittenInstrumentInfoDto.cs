namespace StemStudio.Contracts.InstrumentViews;

/// <summary>One written instrument of the catalogue (backend-api 1.15.0, feature 016, data-model 4.2).</summary>
public sealed record WrittenInstrumentInfoDto
{
    /// <summary>Instrument id.</summary>
    public required WrittenInstrumentDto Id { get; init; }

    /// <summary>Part name (empty for <c>concert</c>: the profile's instrument name).</summary>
    public required string Name { get; init; }

    /// <summary>Instrument key: <c>C</c>, <c>Bb</c>, <c>Eb</c> or <c>F</c>.</summary>
    public required string KeyName { get; init; }

    /// <summary>Sounding to written interval in music21 notation (<c>M6</c>, <c>-m3</c>).</summary>
    public required string Interval { get; init; }

    /// <summary>Default clef (<c>auto</c> for <c>concert</c>: the profile's clef).</summary>
    public required string DefaultClef { get; init; }

    /// <summary>Lowest sounding MIDI pitch (0 for <c>concert</c>: the profile's range).</summary>
    public required int LowMidi { get; init; }

    /// <summary>Highest sounding MIDI pitch (127 for <c>concert</c>).</summary>
    public required int HighMidi { get; init; }
}
