using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Assets;

/// <summary>Role of a file of a library item (1.5.0: <c>audio</c>; feature 008 adds score files).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssetFileRoleDto>))]
public enum AssetFileRoleDto
{
    /// <summary>The audio file of an audio item.</summary>
    [JsonStringEnumMemberName("audio")]
    Audio,
}
