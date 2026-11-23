using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Scores;

/// <summary>Instrument profile of a score (backend contract 1.7.0, data-model.md 2.1 of feature 008).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScoreProfileDto>))]
public enum ScoreProfileDto
{
    /// <summary>One melody line, treble clef.</summary>
    [JsonStringEnumMemberName("vocals")]
    Vocals,

    /// <summary>One note at a time, bass clef 8vb.</summary>
    [JsonStringEnumMemberName("bass")]
    Bass,

    /// <summary>Treble clef 8vb, chords allowed.</summary>
    [JsonStringEnumMemberName("guitar")]
    Guitar,

    /// <summary>Grand staff split at middle C.</summary>
    [JsonStringEnumMemberName("keys")]
    Keys,

    /// <summary>Grand staff; accurate (piano model) or fast quality.</summary>
    [JsonStringEnumMemberName("piano")]
    Piano,

    /// <summary>Percussion staff from the five drum detail stems.</summary>
    [JsonStringEnumMemberName("drums")]
    Drums,

    /// <summary>One line of a string section (1.15.0, feature 016).</summary>
    [JsonStringEnumMemberName("strings")]
    Strings,

    /// <summary>One line of a brass section (1.15.0, feature 016).</summary>
    [JsonStringEnumMemberName("brass")]
    Brass,

    /// <summary>One line of a woodwind section (1.15.0, feature 016).</summary>
    [JsonStringEnumMemberName("woodwinds")]
    Woodwinds,
}
