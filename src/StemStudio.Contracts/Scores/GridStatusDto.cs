using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Scores;

/// <summary>Whether the track's beat grid can be used as the time base.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GridStatusDto>))]
public enum GridStatusDto
{
    /// <summary>The beat grid is used.</summary>
    [JsonStringEnumMemberName("reliable")]
    Reliable,

    /// <summary>A manual tempo is required.</summary>
    [JsonStringEnumMemberName("unreliable")]
    Unreliable,

    /// <summary>No grid (e.g. a library item without a track); a manual tempo is required.</summary>
    [JsonStringEnumMemberName("none")]
    None,
}
