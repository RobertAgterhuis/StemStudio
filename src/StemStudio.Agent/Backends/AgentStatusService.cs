using StemStudio.Agent.Backends.Fakes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>The probes of the status service.</summary>
public sealed record BackendProbes
{
    /// <summary>Ollama probe.</summary>
    public required OllamaBackendProbe Ollama { get; init; }

    /// <summary>Claude probe (sends nothing except on an explicit test).</summary>
    public required ClaudeBackendProbe Claude { get; init; }

    /// <summary>True when the fake backend has a readable script (E2E stack).</summary>
    public bool FakeAvailable { get; init; }
}

/// <summary>
/// <see cref="IAgentStatusService"/>: the status of the selected backend without generating text (FR-023). Ollama is probed
/// (at most 2 s) for every backend so Settings can show it; Claude is never contacted here, only on <see cref="TestAsync(AgentBackendDto, ClaudeKeyTestSourceDto, CancellationToken)"/>.
/// </summary>
/// <param name="settings">Agent settings (the effective backend).</param>
/// <param name="gpu">GPU activity (Ollama fallback model).</param>
/// <param name="probes">Backend probes.</param>
/// <param name="tools">Tool catalogue for the tool list.</param>
public sealed class AgentStatusService(IAgentSettingsStore settings, IGpuActivity gpu, BackendProbes probes, ToolCatalog tools) : IAgentStatusService
{
    /// <summary>Reason while the agent is switched off.</summary>
    public const string OffReason = "The agent is switched off in Settings.";

    /// <summary>Fix while the agent is switched off.</summary>
    public const string OffFix = "Choose Local AI (Ollama) in Settings.";

    /// <summary>Reason when the fake backend has no script.</summary>
    public const string FakeMissingReason = "The fake backend has no readable script (STEMSTUDIO_AGENT_FAKE_SCRIPT).";

    /// <summary>Detail for backends that cannot be tested.</summary>
    public const string NotTestable = "Only the ollama and claude backends can be tested.";

    /// <inheritdoc />
    public async Task<AgentStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        var ollama = await probes.Ollama.ProbeAsync(current.OllamaModel, current.OllamaFallbackModel, cancellationToken);
        var claude = probes.Claude.Status(current);
        var selected = Selected(current, ollama, claude);
        return selected with { Backend = current.Backend, Ollama = ollama.Status, Claude = claude };
    }

    /// <inheritdoc />
    public Task<BackendTestResultDto> TestAsync(AgentBackendDto backend, CancellationToken cancellationToken) =>
        TestAsync(backend, ClaudeKeyTestSourceDto.Active, cancellationToken);

    /// <summary>Tests one backend on the user's explicit request; a Claude test uses the key of the requested source (CK-FR-007).</summary>
    /// <param name="backend">ollama or claude.</param>
    /// <param name="claudeKeySource">Which Claude key to test; ignored for other backends.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result (<c>keySource</c> set for a Claude test with a key).</returns>
    public async Task<BackendTestResultDto> TestAsync(AgentBackendDto backend, ClaudeKeyTestSourceDto claudeKeySource, CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        return backend switch
        {
            AgentBackendDto.Ollama => await TestOllamaAsync(current, cancellationToken),
            AgentBackendDto.Claude => await probes.Claude.TestAsync(current, claudeKeySource, cancellationToken),
            _ => new BackendTestResultDto { Backend = backend, Ok = false, LatencyMs = 0, Detail = NotTestable },
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentToolInfoDto> ListTools(PageContextDto context) => tools.List(context);

    private AgentStatusDto Selected(AgentSettings current, OllamaProbeResult ollama, ClaudeStatusDto claude) => current.Backend switch
    {
        AgentBackendDto.Ollama => OllamaStatus(current, ollama),
        AgentBackendDto.Claude => ClaudeStatus(current, claude),
        AgentBackendDto.Fake => probes.FakeAvailable ? Ready(ScriptedChatClient.ModelId) : NotReady(FakeMissingReason, null),
        _ => NotReady(OffReason, OffFix),
    };

    private AgentStatusDto OllamaStatus(AgentSettings current, OllamaProbeResult ollama)
    {
        var busy = gpu.IsHeavyJobActive();
        if (!ollama.Ready)
        {
            return NotReady(ollama.Reason, ollama.Fix);
        }

        return busy && !ollama.Status.FallbackPresent
            ? NotReady($"Model {current.OllamaFallbackModel} is not pulled.", OllamaBackendProbe.PullCommand(current.OllamaFallbackModel))
            : Ready(busy ? current.OllamaFallbackModel : current.OllamaModel) with { UsingFallback = busy };
    }

    private static AgentStatusDto ClaudeStatus(AgentSettings current, ClaudeStatusDto claude)
    {
        if (!claude.KeyConfigured)
        {
            return NotReady(ClaudeErrors.KeyUnusable(claude.Key.Reason, null), claude.Key.Fix ?? ClaudeErrors.KeyMissingFix);
        }

        return claude.NoticeAcknowledged
            ? Ready(ClaudeChatClientFactory.ModelId(current.ClaudeModel))
            : NotReady(ClaudeErrors.NoticeMissing, "Read and acknowledge the notice in Settings.");
    }

    private async Task<BackendTestResultDto> TestOllamaAsync(AgentSettings current, CancellationToken cancellationToken)
    {
        var started = probes.Ollama.Time.GetTimestamp();
        var result = await probes.Ollama.ProbeAsync(current.OllamaModel, current.OllamaFallbackModel, cancellationToken);
        return new BackendTestResultDto
        {
            Backend = AgentBackendDto.Ollama,
            Ok = result.Ready,
            LatencyMs = (int)probes.Ollama.Time.GetElapsedTime(started).TotalMilliseconds,
            Detail = result.Ready ? $"Ollama {result.Status.Version} answered; model {current.OllamaModel} supports tool calling." : $"{result.Reason} {result.Fix}",
        };
    }

    private static AgentStatusDto Ready(string model) => Empty with { Ready = true, ActiveModel = model };

    private static AgentStatusDto NotReady(string? reason, string? fix) => Empty with { Reason = reason, Fix = fix };

    private static AgentStatusDto Empty { get; } = new()
    {
        Backend = AgentBackendDto.None,
        Ready = false,
        UsingFallback = false,
        Ollama = new OllamaStatusDto { Reachable = false, DefaultPresent = false, FallbackPresent = false },
        Claude = new ClaudeStatusDto { KeyConfigured = false, NoticeAcknowledged = false, Model = ClaudeModelDto.ClaudeOpus55 },
    };
}
