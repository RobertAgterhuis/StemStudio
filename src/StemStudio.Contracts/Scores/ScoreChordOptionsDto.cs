namespace StemStudio.Contracts.Scores;

/// <summary><c>ScoreOptions.chords</c> (backend-api 1.15.0, feature 016).</summary>
public sealed record ScoreChordOptionsDto
{
    /// <summary>Chords are ready and the suggested profile allows chord symbols.</summary>
    public required bool Available { get; init; }
}
