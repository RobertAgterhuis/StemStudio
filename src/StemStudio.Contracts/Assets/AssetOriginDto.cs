using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Assets;

/// <summary>How a library item was made (1.5.0: <c>export</c>; later features add their origins).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssetOriginDto>))]
public enum AssetOriginDto
{
    /// <summary>Exported from a track (feature 007).</summary>
    [JsonStringEnumMemberName("export")]
    Export,

    /// <summary>Transcribed to sheet music (feature 008).</summary>
    [JsonStringEnumMemberName("transcription")]
    Transcription,

    /// <summary>A take of a generation (feature 009).</summary>
    [JsonStringEnumMemberName("generation")]
    Generation,
}
