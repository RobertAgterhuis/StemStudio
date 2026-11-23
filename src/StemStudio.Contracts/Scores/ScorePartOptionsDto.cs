using StemStudio.Contracts.InstrumentViews;

namespace StemStudio.Contracts.Scores;

/// <summary><c>ScoreOptions.parts</c> (backend-api 1.15.0, feature 016).</summary>
public sealed record ScorePartOptionsDto
{
    /// <summary>A written instrument may be chosen.</summary>
    public required bool Available { get; init; }

    /// <summary>Preselected instrument.</summary>
    public required WrittenInstrumentDto DefaultInstrument { get; init; }

    /// <summary>A clef may be chosen.</summary>
    public required bool ClefAllowed { get; init; }
}
