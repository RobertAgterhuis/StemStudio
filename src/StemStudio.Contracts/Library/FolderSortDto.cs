using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Library;

/// <summary>Order of the child folders (backend-api 1.22.0 <c>FolderSort</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FolderSortDto>))]
public enum FolderSortDto
{
    /// <summary>A-Z, a leading "The" ignored, unknown folders last (default for artists and songs).</summary>
    [JsonStringEnumMemberName("name")]
    Name,

    /// <summary>Album year, unknown last (albums of an artist only; their default).</summary>
    [JsonStringEnumMemberName("year")]
    Year,

    /// <summary>Newest item first.</summary>
    [JsonStringEnumMemberName("recent")]
    Recent,

    /// <summary>Largest first.</summary>
    [JsonStringEnumMemberName("size")]
    Size,

    /// <summary>Most items first.</summary>
    [JsonStringEnumMemberName("items")]
    Items,
}
