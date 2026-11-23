using StemStudio.Contracts.InstrumentViews;

namespace StemStudio.Contracts.Scores;

/// <summary><c>tablature</c> of a score request or recipe (backend-api 1.15.0, feature 016, data-model 4.5).</summary>
public sealed record ScoreTablatureDto
{
    /// <summary>Tuning (must match the profile).</summary>
    public required TuningIdDto Tuning { get; init; }

    /// <summary>Capo 0..12 (guitar tunings only).</summary>
    public int Capo { get; init; }

    /// <summary>Layout.</summary>
    public TabLayoutDto Layout { get; init; } = TabLayoutDto.NotationAndTab;
}
