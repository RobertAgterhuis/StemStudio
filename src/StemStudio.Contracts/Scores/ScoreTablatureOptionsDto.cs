using StemStudio.Contracts.InstrumentViews;

namespace StemStudio.Contracts.Scores;

/// <summary><c>ScoreOptions.tablature</c> (backend-api 1.15.0, feature 016).</summary>
public sealed record ScoreTablatureOptionsDto
{
    /// <summary>Tablature is possible for the suggested profile.</summary>
    public required bool Available { get; init; }

    /// <summary>Preselected tuning, or null.</summary>
    public required TuningIdDto? DefaultTuning { get; init; }

    /// <summary>Tunings offered.</summary>
    public required IReadOnlyList<TuningIdDto> Tunings { get; init; }

    /// <summary>A capo may be set (guitar).</summary>
    public required bool CapoAllowed { get; init; }
}
