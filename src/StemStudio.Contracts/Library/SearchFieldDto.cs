using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Library;

/// <summary>Field a search highlight refers to (backend-api 1.22.0 <c>SearchField</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SearchFieldDto>))]
public enum SearchFieldDto
{
    /// <summary>The artist folder name.</summary>
    [JsonStringEnumMemberName("artist")]
    Artist,

    /// <summary>The album folder name.</summary>
    [JsonStringEnumMemberName("album")]
    Album,

    /// <summary>The song folder name.</summary>
    [JsonStringEnumMemberName("song")]
    Song,

    /// <summary>The item name.</summary>
    [JsonStringEnumMemberName("name")]
    Name,

    /// <summary>An instrument label (see <see cref="HighlightDto.Label"/>).</summary>
    [JsonStringEnumMemberName("instrument")]
    Instrument,
}
