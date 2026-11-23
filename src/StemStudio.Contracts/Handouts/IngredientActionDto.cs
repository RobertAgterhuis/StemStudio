using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Handouts;

/// <summary>Where the UI links a missing ingredient to (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<IngredientActionDto>))]
public enum IngredientActionDto
{
    /// <summary>Make sheet music.</summary>
    [JsonStringEnumMemberName("create-score")]
    CreateScore,

    /// <summary>Make tablature.</summary>
    [JsonStringEnumMemberName("create-tab")]
    CreateTab,

    /// <summary>Make drum notation.</summary>
    [JsonStringEnumMemberName("create-drum-notation")]
    CreateDrumNotation,

    /// <summary>Make a transposed part.</summary>
    [JsonStringEnumMemberName("create-transposed-part")]
    CreateTransposedPart,

    /// <summary>Detect chords.</summary>
    [JsonStringEnumMemberName("detect-chords")]
    DetectChords,

    /// <summary>Find sections.</summary>
    [JsonStringEnumMemberName("detect-sections")]
    DetectSections,

    /// <summary>Find lyrics.</summary>
    [JsonStringEnumMemberName("find-lyrics")]
    FindLyrics,

    /// <summary>Split the drums (detail separation).</summary>
    [JsonStringEnumMemberName("split-drums")]
    SplitDrums,

    /// <summary>Write practice notes.</summary>
    [JsonStringEnumMemberName("write-notes")]
    WriteNotes,
}
