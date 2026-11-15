using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Author of a conversation message.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MessageRoleDto>))]
public enum MessageRoleDto
{
    /// <summary>Typed by the user.</summary>
    [JsonStringEnumMemberName("user")]
    User,

    /// <summary>The agent's answer of a run.</summary>
    [JsonStringEnumMemberName("assistant")]
    Assistant,
}
