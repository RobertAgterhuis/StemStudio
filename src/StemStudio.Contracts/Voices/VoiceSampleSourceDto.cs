using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Voices;

/// <summary>How a voice sample was made (1.9.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<VoiceSampleSourceDto>))]
public enum VoiceSampleSourceDto
{
    /// <summary>Recorded in the browser.</summary>
    [JsonStringEnumMemberName("recorded")]
    Recorded,

    /// <summary>Uploaded as a file.</summary>
    [JsonStringEnumMemberName("uploaded")]
    Uploaded,
}
