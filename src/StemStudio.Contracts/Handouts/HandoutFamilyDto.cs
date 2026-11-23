using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Handouts;

/// <summary>Handout family (data-model 6.1; every family counts equally) (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HandoutFamilyDto>))]
public enum HandoutFamilyDto
{
    /// <summary>Band overview.</summary>
    [JsonStringEnumMemberName("overview")]
    Overview,

    /// <summary>Vocals.</summary>
    [JsonStringEnumMemberName("vocals")]
    Vocals,

    /// <summary>Guitar.</summary>
    [JsonStringEnumMemberName("guitar")]
    Guitar,

    /// <summary>Bass.</summary>
    [JsonStringEnumMemberName("bass")]
    Bass,

    /// <summary>Keys.</summary>
    [JsonStringEnumMemberName("keys")]
    Keys,

    /// <summary>Drums.</summary>
    [JsonStringEnumMemberName("drums")]
    Drums,

    /// <summary>Melodic instruments (strings, brass, woodwinds).</summary>
    [JsonStringEnumMemberName("melodic")]
    Melodic,
}
