using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Engine that produced an analysis document (backend-api 1.15.0, feature 016).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnalysisEngineDto>))]
public enum AnalysisEngineDto
{
    /// <summary>BTC chord transformer.</summary>
    [JsonStringEnumMemberName("btc")]
    Btc,

    /// <summary>Fast chord method.</summary>
    [JsonStringEnumMemberName("chroma-hmm")]
    ChromaHmm,

    /// <summary>008 drum onset detection.</summary>
    [JsonStringEnumMemberName("drum-onsets")]
    DrumOnsets,

    /// <summary>pYIN reference melody.</summary>
    [JsonStringEnumMemberName("pyin")]
    Pyin,

    /// <summary>Fast YIN reference melody.</summary>
    [JsonStringEnumMemberName("yin")]
    Yin,

    /// <summary>Deterministic fake (E2E).</summary>
    [JsonStringEnumMemberName("fake")]
    Fake,
}
