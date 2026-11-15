using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Scores;

/// <summary>Mode of a key.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<KeyModeDto>))]
public enum KeyModeDto
{
    /// <summary>Major.</summary>
    [JsonStringEnumMemberName("major")]
    Major,

    /// <summary>Minor.</summary>
    [JsonStringEnumMemberName("minor")]
    Minor,
}
