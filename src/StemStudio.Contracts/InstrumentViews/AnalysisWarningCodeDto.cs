using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Warning code of an analysis (backend-api 1.15.0, feature 016, data-model 2.4, 3.1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnalysisWarningCodeDto>))]
public enum AnalysisWarningCodeDto
{
    /// <summary>The fast method knows only major and minor chords.</summary>
    [JsonStringEnumMemberName("vocabulary-reduced")]
    VocabularyReduced,

    /// <summary>Chord changes are not aligned to beats.</summary>
    [JsonStringEnumMemberName("grid-unreliable")]
    GridUnreliable,

    /// <summary>The chord model was not available; the fast method ran.</summary>
    [JsonStringEnumMemberName("fallback-engine")]
    FallbackEngine,

    /// <summary>Very few chords were detected.</summary>
    [JsonStringEnumMemberName("few-chords")]
    FewChords,

    /// <summary>The key is uncertain.</summary>
    [JsonStringEnumMemberName("key-uncertain")]
    KeyUncertain,

    /// <summary>Very little sung melody was found.</summary>
    [JsonStringEnumMemberName("few-voiced")]
    FewVoiced,

    /// <summary>The source is a detail stem.</summary>
    [JsonStringEnumMemberName("detail-bleed")]
    DetailBleed,
}
