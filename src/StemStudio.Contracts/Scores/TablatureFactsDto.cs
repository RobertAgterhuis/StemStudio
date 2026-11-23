using StemStudio.Contracts.InstrumentViews;

namespace StemStudio.Contracts.Scores;

/// <summary><c>ScoreFacts.tablature</c> (backend-api 1.15.0, feature 016).</summary>
public sealed record TablatureFactsDto
{
    /// <summary>Tuning.</summary>
    public required TuningIdDto Tuning { get; init; }

    /// <summary>Strings (4..6).</summary>
    public required int Strings { get; init; }

    /// <summary>Capo.</summary>
    public required int Capo { get; init; }

    /// <summary>Layout.</summary>
    public required TabLayoutDto Layout { get; init; }

    /// <summary>Notes moved by an octave.</summary>
    public required int OctaveMoved { get; init; }

    /// <summary>Chord notes left out.</summary>
    public required int NotesDropped { get; init; }

    /// <summary>Chords with a wide stretch.</summary>
    public required int WideStretches { get; init; }
}
