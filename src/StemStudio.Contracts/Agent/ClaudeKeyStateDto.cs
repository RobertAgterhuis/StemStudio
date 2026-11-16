using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Summary of the Claude key for the UI (backend API 1.21.0, schema <c>ClaudeKeyState</c>, data-model 3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClaudeKeyStateDto>))]
public enum ClaudeKeyStateDto
{
    /// <summary>No environment variable and no stored key.</summary>
    [JsonStringEnumMemberName("not-configured")]
    NotConfigured,

    /// <summary>The environment variable is set (whatever is stored).</summary>
    [JsonStringEnumMemberName("environment")]
    Environment,

    /// <summary>No environment variable; the stored key decrypts.</summary>
    [JsonStringEnumMemberName("stored")]
    Stored,

    /// <summary>No environment variable; a stored key exists but cannot be decrypted on this installation.</summary>
    [JsonStringEnumMemberName("stored-unreadable")]
    StoredUnreadable,
}
