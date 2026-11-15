using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Artists;

/// <summary>MusicBrainz artist type (backend contract 1.4.0). Serialized as lower-case strings.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ArtistTypeDto>))]
public enum ArtistTypeDto
{
    /// <summary>A band or other group.</summary>
    [JsonStringEnumMemberName("group")]
    Group,

    /// <summary>A single person.</summary>
    [JsonStringEnumMemberName("person")]
    Person,

    /// <summary>An orchestra.</summary>
    [JsonStringEnumMemberName("orchestra")]
    Orchestra,

    /// <summary>A choir.</summary>
    [JsonStringEnumMemberName("choir")]
    Choir,

    /// <summary>A fictional character.</summary>
    [JsonStringEnumMemberName("character")]
    Character,

    /// <summary>Any other type.</summary>
    [JsonStringEnumMemberName("other")]
    Other,
}
