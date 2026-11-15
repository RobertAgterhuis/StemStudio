using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Claude models selectable in the settings; never chosen automatically (CTO decision 2026-11-15).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClaudeModelDto>))]
public enum ClaudeModelDto
{
    /// <summary>Claude Opus 5.5 (default).</summary>
    [JsonStringEnumMemberName("claude-opus-5-5")]
    ClaudeOpus55,

    /// <summary>Claude Sonnet 5.5 (cheaper).</summary>
    [JsonStringEnumMemberName("claude-sonnet-5-5")]
    ClaudeSonnet55,

    /// <summary>Claude Haiku 5.5 (cheapest; Expert level).</summary>
    [JsonStringEnumMemberName("claude-haiku-5-5")]
    ClaudeHaiku55,
}
