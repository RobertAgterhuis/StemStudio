using System.Text.Json;
using Microsoft.Extensions.AI;

namespace StemStudio.Agent.Backends.Fakes;

/// <summary>
/// Fake <see cref="IChatClient"/> that answers each model request with the next scripted turn (research R-10). Used by every
/// unit test (CI has no LLM) and, with <c>STEMSTUDIO_AGENT_BACKEND=fake</c>, by the E2E fake stack. Records every request so
/// tests can assert the messages and tools the runner sent. Not thread-safe: one run at a time.
/// </summary>
public sealed class ScriptedChatClient : IChatClient
{
    /// <summary>Model id reported through <see cref="ChatClientMetadata"/>.</summary>
    public const string ModelId = "scripted";

    private static readonly JsonSerializerOptions ScriptJson = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<ScriptedTurn> _turns;
    private readonly List<ScriptedRequest> _requests = [];
    private readonly ChatClientMetadata _metadata = new("fake", defaultModelId: ModelId);

    /// <summary>Creates a client that plays the given turns in order.</summary>
    /// <param name="turns">The turns.</param>
    public ScriptedChatClient(IEnumerable<ScriptedTurn> turns)
    {
        ArgumentNullException.ThrowIfNull(turns);
        _turns = [.. turns];
    }

    /// <summary>Requests received so far, in order.</summary>
    public IReadOnlyList<ScriptedRequest> Requests => _requests;

    /// <summary>Number of turns not yet played.</summary>
    public int RemainingTurns => _turns.Count - _requests.Count;

    /// <summary>Parses a script file (contracts/agent-script.schema.json).</summary>
    /// <param name="json">Script JSON.</param>
    /// <returns>The client.</returns>
    public static ScriptedChatClient FromJson(string json)
    {
        var script = JsonSerializer.Deserialize<ChatScript>(json, ScriptJson)
            ?? throw new JsonException("The agent script is empty.");
        return script.Version == 1 ? new ScriptedChatClient(script.Turns) : throw new JsonException($"Unsupported agent script version {script.Version}.");
    }

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var turn = Next(messages, options, cancellationToken);
        var message = new ChatMessage(ChatRole.Assistant, Contents(turn));
        return Task.FromResult(new ChatResponse(message) { ModelId = ModelId, Usage = Usage(turn) });
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var turn = Next(messages, options, cancellationToken);
        var updates = Contents(turn).Select(content => new ChatResponseUpdate(ChatRole.Assistant, [content]) { ModelId = ModelId });
        return updates.ToAsyncEnumerable();
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }

        return serviceType == typeof(ChatClientMetadata) ? _metadata : serviceType.IsInstanceOfType(this) ? this : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to release.
    }

    private ScriptedTurn Next(IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();
        if (RemainingTurns <= 0)
        {
            throw new InvalidOperationException($"The agent script has no turn {_requests.Count + 1}.");
        }

        _requests.Add(new ScriptedRequest([.. messages], options));
        return _turns[_requests.Count - 1];
    }

    private List<AIContent> Contents(ScriptedTurn turn)
    {
        var contents = new List<AIContent>();
        if (!string.IsNullOrEmpty(turn.Text))
        {
            contents.Add(new TextContent(turn.Text));
        }

        contents.AddRange(turn.Calls.Select((call, index) => Call(call, index)));
        if (Usage(turn) is { } usage)
        {
            contents.Add(new UsageContent(usage));
        }

        return contents;
    }

    private FunctionCallContent Call(ScriptedCall call, int index) => call.ToContent($"call-{_requests.Count}-{index + 1}");

    private static UsageDetails? Usage(ScriptedTurn turn) => turn.Usage is not { } usage ? null : new UsageDetails
    {
        InputTokenCount = usage.InputTokens,
        OutputTokenCount = usage.OutputTokens,
        CachedInputTokenCount = usage.CachedInputTokens,
        TotalTokenCount = usage.InputTokens + usage.OutputTokens,
    };
}

/// <summary>A request received by <see cref="ScriptedChatClient"/>.</summary>
/// <param name="Messages">Messages as sent (copied).</param>
/// <param name="Options">Options as sent (tools, tool mode, model).</param>
public sealed record ScriptedRequest(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options);
