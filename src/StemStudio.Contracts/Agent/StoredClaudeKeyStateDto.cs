using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>State of the stored Claude key, independent of the environment variable (backend API 1.21.0, schema <c>StoredClaudeKeyState</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StoredClaudeKeyStateDto>))]
public enum StoredClaudeKeyStateDto
{
    /// <summary>Nothing is stored.</summary>
    [JsonStringEnumMemberName("absent")]
    Absent,

    /// <summary>A stored key exists and decrypts.</summary>
    [JsonStringEnumMemberName("readable")]
    Readable,

    /// <summary>A stored key exists but cannot be decrypted (key ring missing or different).</summary>
    [JsonStringEnumMemberName("unreadable")]
    Unreadable,
}
