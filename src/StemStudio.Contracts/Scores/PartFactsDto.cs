using StemStudio.Contracts.InstrumentViews;

namespace StemStudio.Contracts.Scores;

/// <summary><c>ScoreFacts.part</c> (backend-api 1.15.0, feature 016).</summary>
public sealed record PartFactsDto
{
    /// <summary>Written instrument.</summary>
    public required WrittenInstrumentDto Instrument { get; init; }

    /// <summary>Part name.</summary>
    public required string Name { get; init; }

    /// <summary>Sounding to written interval.</summary>
    public required string Interval { get; init; }

    /// <summary>Written key (music21 spelling), or null for drums.</summary>
    public required string? WrittenKey { get; init; }

    /// <summary>Clef written.</summary>
    public required string Clef { get; init; }

    /// <summary>Notes outside the usual range.</summary>
    public required int OutOfRange { get; init; }
}
