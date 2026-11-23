using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Handouts;

/// <summary>Paper size (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PaperDto>))]
public enum PaperDto
{
    /// <summary>A4.</summary>
    [JsonStringEnumMemberName("a4")]
    A4,

    /// <summary>US Letter.</summary>
    [JsonStringEnumMemberName("letter")]
    Letter,
}
