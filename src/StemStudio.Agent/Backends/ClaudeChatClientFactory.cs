using Anthropic;
using Microsoft.Extensions.AI;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>
/// Creates Claude chat clients (optional paid backend, constitution 1.3.0) with the verified options of research F-14: the
/// model id of the setting, adaptive thinking, <c>output_config.effort</c> from the setting and at most 8,000 output tokens
/// per turn. The key is read from <c>ANTHROPIC_API_KEY</c> through <see cref="IEnvironmentReader"/> only, per client, and
/// handed to the SDK and nowhere else (SC-006). The SDK does not retry: a failure ends the run with a readable message.
/// </summary>
/// <param name="environment">Environment reader (the only source of the key).</param>
/// <param name="http">HTTP client for tests (stub handler, base address); null uses the SDK's own client.</param>
public sealed class ClaudeChatClientFactory(IEnvironmentReader environment, HttpClient? http = null)
{
    /// <summary>The environment variable holding the key.</summary>
    public const string KeyVariable = "ANTHROPIC_API_KEY";

    /// <summary>Output token cap of one turn.</summary>
    public const int MaxOutputTokensPerTurn = 8_000;

    private readonly Lock _gate = new();
    private (string Key, AnthropicClient Client)? _cached;

    /// <summary>True when the key is present (never returns or logs the value).</summary>
    public bool KeyConfigured => !string.IsNullOrWhiteSpace(environment.GetVariable(KeyVariable));

    /// <summary>The API model id of a model setting.</summary>
    /// <param name="model">Setting.</param>
    /// <returns>The exact id, e.g. <c>claude-opus-5-5</c>.</returns>
    public static string ModelId(ClaudeModelDto model) => model switch
    {
        ClaudeModelDto.ClaudeSonnet55 => "claude-sonnet-5-5",
        ClaudeModelDto.ClaudeHaiku55 => "claude-haiku-5-5",
        _ => "claude-opus-5-5",
    };

    /// <summary>A chat client for a model; failures and refusals surface as readable run failures.</summary>
    /// <param name="model">Model id.</param>
    /// <returns>The client.</returns>
    /// <exception cref="BackendUnavailableException">The key is not set.</exception>
    public IChatClient Create(string model) =>
        new ClaudeErrorMapping(Client().AsIChatClient(model, MaxOutputTokensPerTurn, AnthropicThinkingMode.Adaptive));

    /// <summary>The backend options of a turn.</summary>
    /// <param name="effort">Effort setting.</param>
    /// <param name="maxOutputTokens">Output tokens allowed in this turn.</param>
    /// <returns>The options (the runner adds tools and tool mode auto).</returns>
    public static ChatOptions Options(ClaudeEffortDto effort, int maxOutputTokens) => new()
    {
        MaxOutputTokens = maxOutputTokens,
        Reasoning = new ReasoningOptions { Effort = Effort(effort) },
    };

    /// <summary>The SDK client for the current key (one per key value, so connections are reused across turns).</summary>
    /// <returns>The client.</returns>
    /// <exception cref="BackendUnavailableException">The key is not set.</exception>
    internal AnthropicClient Client()
    {
        var key = environment.GetVariable(KeyVariable);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BackendUnavailableException(ClaudeErrors.KeyMissing);
        }

        return Cached(key);
    }

    private AnthropicClient Cached(string key)
    {
        lock (_gate)
        {
            if (_cached is not { } cached || !string.Equals(cached.Key, key, StringComparison.Ordinal))
            {
                _cached = cached = (key, NewClient(key));
            }

            return cached.Client;
        }
    }

    private AnthropicClient NewClient(string key) => http is null
        ? new AnthropicClient { ApiKey = key, MaxRetries = 0 }
        : new AnthropicClient { ApiKey = key, MaxRetries = 0, HttpClient = http, BaseUrl = http.BaseAddress!.ToString().TrimEnd('/') };

    private static ReasoningEffort Effort(ClaudeEffortDto effort) => effort switch
    {
        ClaudeEffortDto.High => ReasoningEffort.High,
        ClaudeEffortDto.Medium => ReasoningEffort.Medium,
        _ => ReasoningEffort.Low,
    };
}
