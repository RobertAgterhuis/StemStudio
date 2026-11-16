using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>The chat backends available to the selector.</summary>
public sealed record ChatBackends
{
    /// <summary>Local Ollama (default).</summary>
    public required OllamaChatClientFactory Ollama { get; init; }

    /// <summary>Optional paid Claude backend; null when not configured (constitution 1.3.0).</summary>
    public IAgentChatSession? Claude { get; init; }

    /// <summary>Scripted fake for the E2E stack; null outside it.</summary>
    public IAgentChatSession? Fake { get; init; }
}

/// <summary>
/// Picks the client and model of every turn (FR-021): the run's backend; with Ollama the default model on the GPU, or the
/// fallback model on the CPU while a heavy GPU job runs (published as <c>model.changed</c> by the runner). Never switches to
/// Claude on its own.
/// </summary>
/// <param name="settings">Agent settings.</param>
/// <param name="gpu">GPU activity.</param>
/// <param name="backends">Backends.</param>
public sealed class AgentModelSelector(IAgentSettingsStore settings, IGpuActivity gpu, ChatBackends backends) : IAgentChatSession
{
    /// <summary>Change reason when the fallback model takes over.</summary>
    public const string FallbackReason = "A GPU job is running: the agent uses the smaller model on the CPU.";

    /// <inheritdoc />
    public async Task<ChatTurn> NextTurnAsync(ChatTurnContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Run.Backend switch
        {
            AgentBackendDto.Ollama => await OllamaAsync(cancellationToken),
            AgentBackendDto.None => throw new BackendUnavailableException("The agent is switched off in Settings."),
            AgentBackendDto.Claude => await (backends.Claude ?? throw new BackendUnavailableException("The Claude backend is not configured.")).NextTurnAsync(context, cancellationToken),
            _ => await (backends.Fake ?? throw new BackendUnavailableException("No language model is available for this run.")).NextTurnAsync(context, cancellationToken),
        };
    }

    private async Task<ChatTurn> OllamaAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        var busy = gpu.IsHeavyJobActive();
        var model = busy ? current.OllamaFallbackModel : current.OllamaModel;
        return new ChatTurn(backends.Ollama.Create(model), model, busy)
        {
            ChangeReason = busy ? FallbackReason : null,
            Options = OllamaChatClientFactory.Options(current.OllamaThink, busy),
            TimeoutSeconds = current.Limits.TurnTimeoutSeconds,
        };
    }
}
