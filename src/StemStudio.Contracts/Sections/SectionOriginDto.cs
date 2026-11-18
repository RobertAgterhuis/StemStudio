using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Sections;

/// <summary>Who made one section (backend-api 1.13.0, feature 014).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SectionOriginDto>))]
public enum SectionOriginDto
{
    /// <summary>The analyser, unchanged.</summary>
    [JsonStringEnumMemberName("detected")]
    Detected,

    /// <summary>Added or changed by the user.</summary>
    [JsonStringEnumMemberName("user")]
    User,
}
