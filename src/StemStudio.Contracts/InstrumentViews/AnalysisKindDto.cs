using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Kind of a per-track analysis (backend-api 1.15.0, feature 016).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnalysisKindDto>))]
public enum AnalysisKindDto
{
    /// <summary>Chord chart with the song key.</summary>
    [JsonStringEnumMemberName("chords")]
    Chords,

    /// <summary>Drum hit map.</summary>
    [JsonStringEnumMemberName("drumHits")]
    DrumHits,

    /// <summary>Reference melody.</summary>
    [JsonStringEnumMemberName("vocalPitch")]
    VocalPitch,
}
