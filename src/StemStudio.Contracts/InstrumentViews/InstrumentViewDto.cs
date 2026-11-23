using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>The view a family opens (backend-api 1.15.0, feature 016).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InstrumentViewDto>))]
public enum InstrumentViewDto
{
    /// <summary>Singing feedback.</summary>
    [JsonStringEnumMemberName("singing")]
    Singing,

    /// <summary>Drum trainer.</summary>
    [JsonStringEnumMemberName("drum-trainer")]
    DrumTrainer,

    /// <summary>Tablature and chords.</summary>
    [JsonStringEnumMemberName("tablature")]
    Tablature,

    /// <summary>Chord lane and chord sheet.</summary>
    [JsonStringEnumMemberName("chords")]
    Chords,

    /// <summary>Transposed part.</summary>
    [JsonStringEnumMemberName("part")]
    Part,
}
