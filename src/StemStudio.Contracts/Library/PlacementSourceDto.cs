using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Library;

/// <summary>Where a filing name comes from (backend-api 1.22.0 <c>PlacementSource</c>; precedence item &gt; track &gt; match &gt; import &gt; fallback).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlacementSourceDto>))]
public enum PlacementSourceDto
{
    /// <summary>Set by the user for this item (library info of the item).</summary>
    [JsonStringEnumMemberName("item")]
    Item,

    /// <summary>Set by the user for its track (library info of the track).</summary>
    [JsonStringEnumMemberName("track")]
    Track,

    /// <summary>The band info match (feature 005).</summary>
    [JsonStringEnumMemberName("match")]
    Match,

    /// <summary>The names reported by the import (lookup names, then track names, then the provenance snapshot).</summary>
    [JsonStringEnumMemberName("import")]
    Import,

    /// <summary>A fixed fallback ("Unknown artist", "Unknown album", the track title, "My songs").</summary>
    [JsonStringEnumMemberName("fallback")]
    Fallback,
}
