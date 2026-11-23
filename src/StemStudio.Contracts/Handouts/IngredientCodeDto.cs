using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Handouts;

/// <summary>Handout ingredient (data-model 6.2) (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<IngredientCodeDto>))]
public enum IngredientCodeDto
{
    /// <summary>Tempo, metre, key, duration, artwork.</summary>
    [JsonStringEnumMemberName("facts")]
    Facts,

    /// <summary>Sections with bars and practice tempo.</summary>
    [JsonStringEnumMemberName("form-chart")]
    FormChart,

    /// <summary>Lyrics with chords above the words.</summary>
    [JsonStringEnumMemberName("chords-over-lyrics")]
    ChordsOverLyrics,

    /// <summary>Plain lyrics (cues for drums).</summary>
    [JsonStringEnumMemberName("lyrics")]
    Lyrics,

    /// <summary>Chord chart per section and bar.</summary>
    [JsonStringEnumMemberName("chord-chart")]
    ChordChart,

    /// <summary>Score, tablature, drum notation or transposed part.</summary>
    [JsonStringEnumMemberName("notation")]
    Notation,

    /// <summary>The user's practice notes for the family.</summary>
    [JsonStringEnumMemberName("practice-notes")]
    PracticeNotes,

    /// <summary>Tempo reached per section (014 log).</summary>
    [JsonStringEnumMemberName("practice-progress")]
    PracticeProgress,

    /// <summary>Setlist and entry notes (setlist handouts).</summary>
    [JsonStringEnumMemberName("setlist-notes")]
    SetlistNotes,
}
