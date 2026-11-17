using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Sections;

/// <summary>Who made the current section list (backend-api 1.13.0, feature 014).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SectionSetOriginDto>))]
public enum SectionSetOriginDto
{
    /// <summary>The analyser.</summary>
    [JsonStringEnumMemberName("detected")]
    Detected,

    /// <summary>The user (after the first successful replace).</summary>
    [JsonStringEnumMemberName("edited")]
    Edited,
}
