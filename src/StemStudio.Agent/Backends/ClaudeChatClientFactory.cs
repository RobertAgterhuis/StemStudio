using Anthropic;
using Microsoft.Extensions.AI;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>
/// Creates Claude chat clients (optional paid backend, constitution 1.4.0) with the verified options of research F-14: the
/// model id of the setting, adaptive thinking, <c>output_config.effort</c> from the setting and at most 8,000 output tokens
/// per turn. The key comes only from <see cref="IClaudeKeyProvider"/> (environment variable over stored key, 011 follow-up
/// claude-key) and is handed to the SDK and nowhere else (SC-006, CK-FR-009). The SDK client is cached per key fingerprint.
/// The SDK does not retry: a failure ends the run with a readable message.
/// </summary>
/// <param name="keys">The single source of the key.</param>
/// <param name="http">HTTP client for tests (stub handler, base address); null uses the SDK's own client.</param>
public sealed class ClaudeChatClientFactory(IClaudeKeyProvider keys, HttpClient? http = null)
{
    /// <summary>The environment variable holding the key.</summary>
    public const string KeyVariable = ClaudeKeyProvider.KeyVariable;

    /// <summary>Output token cap of one turn.</summary>
    public const int MaxOutputTokensPerTurn = 8_000;

    private readonly Lock _gate = new();
    private (string Fingerprint, AnthropicClient Client)? _cached;

    /// <summary>The key provider (status and explicit tests read the key state through it).</summary>
    public IClaudeKeyProvider Keys { get; } = keys ?? throw new ArgumentNullException(nameof(keys));

    /// <summary>The API model id of a model setting.</summary>
    /// <param name="model">Setting.</param>
    /// <returns>The exact id, e.g. <c>claude-opus-5-5</c>.</returns>
    public static string ModelId(ClaudeModelDto model) => model switch
    {
        ClaudeModelDto.ClaudeSonnet55 => "claude-sonnet-5-5",
        ClaudeModelDto.ClaudeHaiku55 => "claude-haiku-5-5",
        _ => "claude-opus-5-5",
    };

    /// <summary>A chat client for a model with the active key; failures and refusals surface as readable run failures.</summary>
    /// <param name="model">Model id.</param>
    /// <returns>The client.</returns>
    /// <exception cref="BackendUnavailableException">No usable key (the message names the reason of the key state).</exception>
    public IChatClient Create(string model) => Create(model, ActiveKey());

    /// <summary>A chat client for a model with a given key (a run keeps the key of its first turn, CK-FR-016).</summary>
    /// <param name="model">Model id.</param>
    /// <param name="key">The key.</param>
    /// <returns>The client.</returns>
    public IChatClient Create(string model, ClaudeApiKey key) =>
        new ClaudeErrorMapping(Client(key).AsIChatClient(model, MaxOutputTokensPerTurn, AnthropicThinkingMode.Adaptive));

    /// <summary>The key in use now.</summary>
    /// <returns>The key.</returns>
    /// <exception cref="BackendUnavailableException">No usable key (the message names the reason of the key state).</exception>
    public ClaudeApiKey ActiveKey()
    {
        var resolution = Keys.Resolve();
        return resolution.Key ?? throw new BackendUnavailableException(ClaudeErrors.KeyUnusable(resolution.Reason, resolution.Fix));
    }

    /// <summary>The backend options of a turn.</summary>
    /// <param name="effort">Effort setting.</param>
    /// <param name="maxOutputTokens">Output tokens allowed in this turn.</param>
    /// <returns>The options (the runner adds tools and tool mode auto).</returns>
    public static ChatOptions Options(ClaudeEffortDto effort, int maxOutputTokens) => new()
    {
        MaxOutputTokens = maxOutputTokens,
        Reasoning = new ReasoningOptions { Effort = Effort(effort) },
    };

    /// <summary>The SDK client of a key; one cached client per key fingerprint, so connections are reused across turns.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The client.</returns>
    internal AnthropicClient Client(ClaudeApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            if (_cached is not { } cached || !string.Equals(cached.Fingerprint, key.Fingerprint, StringComparison.Ordinal))
            {
                _cached = cached = (key.Fingerprint, NewClient(key.Reveal()));
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
