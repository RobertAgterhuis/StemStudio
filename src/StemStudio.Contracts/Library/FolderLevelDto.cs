using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Library;

/// <summary>Level of a library folder (backend-api 1.22.0 <c>FolderLevel</c>, feature 022).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FolderLevelDto>))]
public enum FolderLevelDto
{
    /// <summary>An artist folder (id prefix <c>ar</c>).</summary>
    [JsonStringEnumMemberName("artist")]
    Artist,

    /// <summary>An album folder inside an artist (id prefix <c>al</c>).</summary>
    [JsonStringEnumMemberName("album")]
    Album,

    /// <summary>A song folder inside an album; it holds the items (id prefix <c>so</c>).</summary>
    [JsonStringEnumMemberName("song")]
    Song,
}
