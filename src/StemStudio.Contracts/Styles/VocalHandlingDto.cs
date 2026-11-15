using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Styles;

/// <summary>What happens with the vocals (1.8.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<VocalHandlingDto>))]
public enum VocalHandlingDto
{
    /// <summary>The vocals stay in the source mix.</summary>
    [JsonStringEnumMemberName("keep")]
    Keep,

    /// <summary>The vocal stem is excluded; the caption says instrumental.</summary>
    [JsonStringEnumMemberName("instrumental")]
    Instrumental,
}
