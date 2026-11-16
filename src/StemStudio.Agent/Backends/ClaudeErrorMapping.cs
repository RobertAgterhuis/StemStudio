using System.Net;
using System.Runtime.CompilerServices;
using Anthropic.Exceptions;
using Microsoft.Extensions.AI;
using StemStudio.Agent.Runtime;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>The readable texts of Claude failures; each offers the way back to the local model (FR-020).</summary>
public static class ClaudeErrors
{
    /// <summary>Appended to every failure text (the panel shows "Switch to local" next to it).</summary>
    public const string SwitchToLocal = "Switch to local to continue with the model on this computer.";

    /// <summary>No key is set: neither <c>ANTHROPIC_API_KEY</c> nor a stored key (011 follow-up claude-key).</summary>
    public const string KeyMissing = "No Claude key is configured: enter it in Settings or set ANTHROPIC_API_KEY. " + SwitchToLocal;

    /// <summary>Fix shown with <see cref="KeyMissing"/> in the agent status.</summary>
    public const string KeyMissingFix = "Enter the Claude key in Settings, or set ANTHROPIC_API_KEY in the shell that starts StemStudio and restart it.";

    /// <summary>
    /// The failure text when no usable key is available: the readable reason of the key state (unreadable stored key,
    /// unusable key store, malformed environment value) with its fix, or <see cref="KeyMissing"/>. Never contains a key.
    /// </summary>
    /// <param name="reason">Reason of the key state, or null when simply nothing is configured.</param>
    /// <param name="fix">Fix of the key state; left out when null.</param>
    /// <returns>The text, ending with <see cref="SwitchToLocal"/>.</returns>
    public static string KeyUnusable(string? reason, string? fix) =>
        reason is null ? KeyMissing : string.Join(' ', fix is null ? [reason, SwitchToLocal] : [reason, fix, SwitchToLocal]);

    /// <summary>The paid-service notice was not acknowledged.</summary>
    public const string NoticeMissing = "The notice about the paid Claude service is not acknowledged in Settings. " + SwitchToLocal;

    /// <summary>Claude declined the request.</summary>
    public const string Refusal = "Claude declined this request. " + SwitchToLocal;

    /// <summary>No connection.</summary>
    public const string Unreachable = "Claude could not be reached; check the internet connection. " + SwitchToLocal;

    /// <summary>The per-run token budget is used up.</summary>
    /// <param name="input">Input tokens used.</param>
    /// <param name="output">Output tokens used.</param>
    /// <returns>The text.</returns>
    public static string BudgetExhausted(long input, long output) =>
        $"The token budget of this run is used up ({input} input, {output} output tokens). " + SwitchToLocal;

    /// <summary>The text of an HTTP error status.</summary>
    /// <param name="status">Status code.</param>
    /// <returns>The text with the status number; never the response body (it may echo request data).</returns>
    public static string ForStatus(HttpStatusCode status)
    {
        var code = (int)status;
        var reason = code switch
        {
            401 or 403 => "Claude rejected the API key",
            429 => "Claude is rate limited or the account has no credit left",
            >= 500 => "Claude is temporarily unavailable",
            _ => "Claude rejected the request",
        };
        return $"{reason} (HTTP {code}). " + SwitchToLocal;
    }
}

/// <summary>
/// Maps SDK failures of a streamed Claude turn to run failures: HTTP errors and connection problems to
/// <c>backend-unavailable</c>, <c>stop_reason: refusal</c> to <c>refusal</c>. The SDK exception is not kept as the cause
/// because its message can contain the response body.
/// </summary>
/// <param name="inner">The SDK chat client.</param>
internal sealed class ClaudeErrorMapping(IChatClient inner) : DelegatingChatClient(inner)
{
    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var updates = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var refused = false;
        while (await NextAsync(updates))
        {
            refused |= updates.Current.FinishReason == ChatFinishReason.ContentFilter;
            yield return updates.Current;
        }

        if (refused)
        {
            throw new RunStopException(AgentFailureCodes.Refusal, ClaudeErrors.Refusal);
        }
    }

    private static async Task<bool> NextAsync(IAsyncEnumerator<ChatResponseUpdate> updates)
    {
        try
        {
            return await updates.MoveNextAsync();
        }
        catch (AnthropicApiException exception)
        {
            throw new RunStopException(AgentFailureCodes.BackendUnavailable, ClaudeErrors.ForStatus(exception.StatusCode));
        }
        catch (Exception exception) when (exception is AnthropicIOException or HttpRequestException)
        {
            throw new RunStopException(AgentFailureCodes.BackendUnavailable, ClaudeErrors.Unreachable);
        }
    }
}
