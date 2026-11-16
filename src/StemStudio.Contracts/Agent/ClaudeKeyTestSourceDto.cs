using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Which key a Claude connection test uses (backend API 1.21.0, schema <c>ClaudeKeyTestSource</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClaudeKeyTestSourceDto>))]
public enum ClaudeKeyTestSourceDto
{
    /// <summary>The key in use: the environment variable first, else the stored key (default).</summary>
    [JsonStringEnumMemberName("active")]
    Active,

    /// <summary>The stored key, also while the environment variable overrides it.</summary>
    [JsonStringEnumMemberName("stored")]
    Stored,

    /// <summary>The environment variable.</summary>
    [JsonStringEnumMemberName("environment")]
    Environment,
}
