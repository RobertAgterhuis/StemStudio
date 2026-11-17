using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Library;

/// <summary>Kind of folder art (backend-api 1.22.0 <c>FolderArtKind</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FolderArtKindDto>))]
public enum FolderArtKindDto
{
    /// <summary>One picture.</summary>
    [JsonStringEnumMemberName("cover")]
    Cover,

    /// <summary>Two to four album covers (artist folders).</summary>
    [JsonStringEnumMemberName("mosaic")]
    Mosaic,

    /// <summary>No picture: a lettered placeholder.</summary>
    [JsonStringEnumMemberName("placeholder")]
    Placeholder,
}
