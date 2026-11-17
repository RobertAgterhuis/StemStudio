using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Whether StemStudio can recreate data (<c>Regenerability</c>) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RegenerabilityDto>))]
public enum RegenerabilityDto
{
    /// <summary>StemStudio cannot recreate it.</summary>
    [JsonStringEnumMemberName("original")]
    Original,

    /// <summary>One action (or automatic use) recreates it.</summary>
    [JsonStringEnumMemberName("regenerable")]
    Regenerable,
}
