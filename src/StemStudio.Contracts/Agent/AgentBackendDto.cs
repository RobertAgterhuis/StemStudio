using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Language-model backend of the agent (agent API delta, target backend API 1.10.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentBackendDto>))]
public enum AgentBackendDto
{
    /// <summary>Local open-weight model served by Ollama on the host (default).</summary>
    [JsonStringEnumMemberName("ollama")]
    Ollama,

    /// <summary>Anthropic Claude API: paid, opt-in, key only from ANTHROPIC_API_KEY (constitution 1.3.0).</summary>
    [JsonStringEnumMemberName("claude")]
    Claude,

    /// <summary>Agent switched off; the rest of the app works unchanged.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Scripted chat client for tests and the E2E fake stack only.</summary>
    [JsonStringEnumMemberName("fake")]
    Fake,
}
