using StemStudio.Contracts.InstrumentViews;

namespace StemStudio.Contracts.Scores;

/// <summary><c>part</c> of a score request or recipe (backend-api 1.15.0, feature 016, data-model 4.6).</summary>
public sealed record ScorePartDto
{
    /// <summary>Written instrument.</summary>
    public required WrittenInstrumentDto Instrument { get; init; }

    /// <summary>Clef (<c>auto</c> for keys/piano).</summary>
    public ClefChoiceDto Clef { get; init; } = ClefChoiceDto.Auto;
}
