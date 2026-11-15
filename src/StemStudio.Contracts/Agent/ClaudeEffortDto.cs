using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Claude effort (output_config.effort); thinking is adaptive and cannot be disabled on Opus 5.5.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClaudeEffortDto>))]
public enum ClaudeEffortDto
{
    /// <summary>Default for chat-style commands.</summary>
    [JsonStringEnumMemberName("low")]
    Low,

    /// <summary>More thorough.</summary>
    [JsonStringEnumMemberName("medium")]
    Medium,

    /// <summary>Most thorough; slower and more tokens.</summary>
    [JsonStringEnumMemberName("high")]
    High,
}
