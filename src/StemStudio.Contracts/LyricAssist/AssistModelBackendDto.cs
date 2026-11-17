using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Model backend of the assistant (<c>LyricAssistStatus.model.backend</c>; mirrors the 011 agent backend).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssistModelBackendDto>))]
public enum AssistModelBackendDto
{
    /// <summary>Local Ollama.</summary>
    [JsonStringEnumMemberName("ollama")]
    Ollama,

    /// <summary>Claude (opt-in, notice required).</summary>
    [JsonStringEnumMemberName("claude")]
    Claude,

    /// <summary>No model backend: dictionary layer only.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Scripted model (tests and E2E).</summary>
    [JsonStringEnumMemberName("fake")]
    Fake,
}
