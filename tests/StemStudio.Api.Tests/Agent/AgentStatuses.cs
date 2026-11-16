using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Tests.Agent;

/// <summary>Agent status DTOs as the agent library's status service reports them.</summary>
internal static class AgentStatuses
{
    public static AgentStatusDto Ready() => new()
    {
        Backend = AgentBackendDto.Ollama,
        Ready = true,
        ActiveModel = "qwen3.5:9b",
        UsingFallback = false,
        Ollama = new OllamaStatusDto
        {
            Reachable = true,
            Version = "0.40.16",
            Models = [new OllamaModelInfoDto { Name = "qwen3.5:9b", SizeBytes = 6_594_474_711, Tools = true }],
            DefaultPresent = true,
            FallbackPresent = false,
        },
        Claude = new ClaudeStatusDto { KeyConfigured = false, NoticeAcknowledged = false, Model = ClaudeModelDto.ClaudeOpus55 },
    };

    public static AgentStatusDto NotReady(AgentBackendDto backend, string reason, string? fix) => Ready() with
    {
        Backend = backend,
        Ready = false,
        ActiveModel = null,
        Reason = reason,
        Fix = fix,
        Ollama = Ready().Ollama with { Reachable = backend != AgentBackendDto.Ollama || fix is not null && fix.StartsWith("ollama pull", StringComparison.Ordinal), Models = [] },
    };
}
