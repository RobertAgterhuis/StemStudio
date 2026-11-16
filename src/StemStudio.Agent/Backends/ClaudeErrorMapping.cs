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

    /// <summary>The key is not set.</summary>
    public const string KeyMissing = "ANTHROPIC_API_KEY is not set in the environment of StemStudio. " + SwitchToLocal;

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
