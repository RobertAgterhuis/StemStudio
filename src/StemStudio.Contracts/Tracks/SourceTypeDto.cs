using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Tracks;

/// <summary>Where the audio of a track came from. Serialized as lower-case strings.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceTypeDto>))]
public enum SourceTypeDto
{
    /// <summary>A YouTube video.</summary>
    [JsonStringEnumMemberName("youtube")]
    Youtube,

    /// <summary>A Spotify track whose audio is matched on YouTube.</summary>
    [JsonStringEnumMemberName("spotify")]
    Spotify,

    /// <summary>A local file upload.</summary>
    [JsonStringEnumMemberName("upload")]
    Upload,
}
