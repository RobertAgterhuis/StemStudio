using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Scores;

/// <summary>Transcription engine of a score.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScoreEngineDto>))]
public enum ScoreEngineDto
{
    /// <summary>Spotify basic-pitch (ONNX, CPU).</summary>
    [JsonStringEnumMemberName("basic-pitch")]
    BasicPitch,

    /// <summary>ByteDance high-resolution piano transcription.</summary>
    [JsonStringEnumMemberName("piano-crnn")]
    PianoCrnn,

    /// <summary>Onset detection on the drum detail stems.</summary>
    [JsonStringEnumMemberName("drum-onsets")]
    DrumOnsets,

    /// <summary>Deterministic fake transcriber (fake stack).</summary>
    [JsonStringEnumMemberName("fake")]
    Fake,
}
