using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Tracks;

/// <summary>Where a track came from (1.8.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TrackOriginKindDto>))]
public enum TrackOriginKindDto
{
    /// <summary>Imported from a URL or an upload (the default; origin absent).</summary>
    [JsonStringEnumMemberName("imported")]
    Imported,

    /// <summary>Imported from a generated take.</summary>
    [JsonStringEnumMemberName("generated")]
    Generated,
}
