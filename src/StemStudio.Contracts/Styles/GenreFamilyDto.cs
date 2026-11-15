using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Styles;

/// <summary>Genre family of a style (1.8.0, 009 data-model.md 3.1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GenreFamilyDto>))]
public enum GenreFamilyDto
{
    /// <summary>Rock.</summary>
    [JsonStringEnumMemberName("rock")]
    Rock,

    /// <summary>Pop.</summary>
    [JsonStringEnumMemberName("pop")]
    Pop,

    /// <summary>Jazz.</summary>
    [JsonStringEnumMemberName("jazz")]
    Jazz,

    /// <summary>Blues.</summary>
    [JsonStringEnumMemberName("blues")]
    Blues,

    /// <summary>Electronic.</summary>
    [JsonStringEnumMemberName("electronic")]
    Electronic,

    /// <summary>Hip hop.</summary>
    [JsonStringEnumMemberName("hipHop")]
    HipHop,

    /// <summary>Latin.</summary>
    [JsonStringEnumMemberName("latin")]
    Latin,

    /// <summary>Folk.</summary>
    [JsonStringEnumMemberName("folk")]
    Folk,

    /// <summary>Classical.</summary>
    [JsonStringEnumMemberName("classical")]
    Classical,

    /// <summary>World.</summary>
    [JsonStringEnumMemberName("world")]
    World,

    /// <summary>Country.</summary>
    [JsonStringEnumMemberName("country")]
    Country,

    /// <summary>Metal.</summary>
    [JsonStringEnumMemberName("metal")]
    Metal,

    /// <summary>Soul.</summary>
    [JsonStringEnumMemberName("soul")]
    Soul,

    /// <summary>Ambient.</summary>
    [JsonStringEnumMemberName("ambient")]
    Ambient,
}
