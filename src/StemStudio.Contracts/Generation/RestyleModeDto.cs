using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Generation;

/// <summary>How a restyle uses the range (1.8.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RestyleModeDto>))]
public enum RestyleModeDto
{
    /// <summary>The range becomes the take.</summary>
    [JsonStringEnumMemberName("range")]
    Range,

    /// <summary>The whole track is kept and only the range is restyled.</summary>
    [JsonStringEnumMemberName("inPlace")]
    InPlace,
}
