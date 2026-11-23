using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Chord vocabulary (backend-api 1.15.0, feature 016).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChordVocabularyDto>))]
public enum ChordVocabularyDto
{
    /// <summary>Major and minor (25 classes).</summary>
    [JsonStringEnumMemberName("simple")]
    Simple,

    /// <summary>170 classes plus inversions (Advanced).</summary>
    [JsonStringEnumMemberName("full")]
    Full,
}
