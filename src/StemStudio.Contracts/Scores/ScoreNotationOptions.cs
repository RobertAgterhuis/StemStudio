using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Scores;

/// <summary>
/// The 016 notation options shared by <see cref="CreateScoreRequest"/> and <see cref="ScoreRecipeDto"/> (backend-api 1.15.0,
/// <c>CreateScoreRequestExtension</c>); every member is absent on the wire when unset.
/// </summary>
public abstract record ScoreNotationOptions
{
    /// <summary>Tablature (guitar/bass, 1.15.0); null = none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScoreTablatureDto? Tablature { get; init; }

    /// <summary>Written instrument and clef (1.15.0); null = concert pitch.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScorePartDto? Part { get; init; }

    /// <summary>Chord symbols above the top staff (1.15.0).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IncludeChords { get; init; }
}
