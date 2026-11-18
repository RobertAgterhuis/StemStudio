using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Recording;

/// <summary>Input channel mode of a take (backend-api 1.14.0, feature 015).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InputChannelModeDto>))]
public enum InputChannelModeDto
{
    /// <summary>Channel 1 of the device as mono.</summary>
    [JsonStringEnumMemberName("mono-1")]
    Mono1,

    /// <summary>Channel 2 of the device as mono.</summary>
    [JsonStringEnumMemberName("mono-2")]
    Mono2,

    /// <summary>Channels 1 and 2 as stereo.</summary>
    [JsonStringEnumMemberName("stereo")]
    Stereo,
}
