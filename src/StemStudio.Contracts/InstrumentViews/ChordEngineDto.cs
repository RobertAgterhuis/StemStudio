using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Chord engine (backend-api 1.15.0, feature 016).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChordEngineDto>))]
public enum ChordEngineDto
{
    /// <summary>BTC transformer (MIT weights).</summary>
    [JsonStringEnumMemberName("btc")]
    Btc,

    /// <summary>Fast chroma + HMM method (major/minor only).</summary>
    [JsonStringEnumMemberName("chroma-hmm")]
    ChromaHmm,

    /// <summary>Deterministic fake (E2E).</summary>
    [JsonStringEnumMemberName("fake")]
    Fake,
}
