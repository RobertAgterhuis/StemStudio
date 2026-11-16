using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Where a Claude API key comes from (backend API 1.21.0, schema <c>ClaudeKeySource</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClaudeKeySourceDto>))]
public enum ClaudeKeySourceDto
{
    /// <summary>The <c>ANTHROPIC_API_KEY</c> environment variable of the API; it wins over a stored key.</summary>
    [JsonStringEnumMemberName("environment")]
    Environment,

    /// <summary>Saved in Settings, stored encrypted (constitution 1.4.0, ADR-035).</summary>
    [JsonStringEnumMemberName("stored")]
    Stored,
}
