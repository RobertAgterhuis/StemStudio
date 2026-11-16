using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>
/// The Claude turns of a run (constitution 1.3.0): only when the key is present and the notice is acknowledged; the model
/// and effort of the settings; the per-run token budget (<c>ClaudeMaxInputTokensPerRun</c>, <c>ClaudeMaxOutputTokensPerRun</c>)
/// ends the run with <c>budget-exhausted</c> before another request is sent, and each turn may use only the remaining
/// output budget.
/// </summary>
/// <param name="settings">Agent settings.</param>
/// <param name="factory">Claude client factory.</param>
public sealed class ClaudeChatSession(IAgentSettingsStore settings, ClaudeChatClientFactory factory) : IAgentChatSession
{
    /// <inheritdoc />
    public async Task<ChatTurn> NextTurnAsync(ChatTurnContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = await settings.GetAsync(cancellationToken);
        EnsureAllowed(current);
        var remaining = RemainingOutput(current.Limits, context.Run.Usage);
        var model = ClaudeChatClientFactory.ModelId(current.ClaudeModel);
        return new ChatTurn(factory.Create(model), model, false)
        {
            Options = ClaudeChatClientFactory.Options(current.ClaudeEffort, remaining),
            TimeoutSeconds = current.Limits.ClaudeTurnTimeoutSeconds,
        };
    }

    private void EnsureAllowed(AgentSettings current)
    {
        if (!factory.KeyConfigured)
        {
            throw new BackendUnavailableException(ClaudeErrors.KeyMissing);
        }

        if (!current.AllowsClaude(keyConfigured: true))
        {
            throw new BackendUnavailableException(ClaudeErrors.NoticeMissing);
        }
    }

    private static int RemainingOutput(AgentLimitsDto limits, TokenUsage usage)
    {
        if (usage.InputTokens >= limits.ClaudeMaxInputTokensPerRun || usage.OutputTokens >= limits.ClaudeMaxOutputTokensPerRun)
        {
            throw new RunStopException(AgentFailureCodes.BudgetExhausted, ClaudeErrors.BudgetExhausted(usage.InputTokens, usage.OutputTokens));
        }

        return (int)Math.Min(ClaudeChatClientFactory.MaxOutputTokensPerTurn, limits.ClaudeMaxOutputTokensPerRun - usage.OutputTokens);
    }
}
