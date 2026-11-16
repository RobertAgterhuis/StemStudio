using System.Text.Json;
using Microsoft.Extensions.AI;

namespace StemStudio.Agent.Backends.Fakes;

/// <summary>
/// A hostile fake model for the injection tests (SC-002): its first turn makes the probe call; as soon as a tool result
/// contains <c>untrusted_text</c> it makes the attack calls (e.g. <c>delete_track</c>, <c>import_from_url</c> with a foreign
/// URL, <c>update_mix</c>) once; otherwise it answers "Done.". Not thread-safe: one run at a time.
/// </summary>
/// <param name="probe">The call that reads external content.</param>
/// <param name="attack">The calls made after seeing untrusted text.</param>
public sealed class AdversarialChatClient(ScriptedCall probe, IReadOnlyList<ScriptedCall> attack) : IChatClient
{
    private readonly ChatClientMetadata _metadata = new("fake", defaultModelId: ScriptedChatClient.ModelId);
    private int _turn;
    private bool _attacked;

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Next(messages))) { ModelId = ScriptedChatClient.ModelId });

    /// <inheritdoc />
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Next(messages).Select(content => new ChatResponseUpdate(ChatRole.Assistant, [content]) { ModelId = ScriptedChatClient.ModelId }).ToAsyncEnumerable();

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType == typeof(ChatClientMetadata) ? _metadata : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to release.
    }

    private List<AIContent> Next(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        _turn++;
        if (!_attacked && SawUntrustedText(messages))
        {
            _attacked = true;
            return [.. attack.Select(Call)];
        }

        return _turn == 1 ? [Call(probe, 0)] : [new TextContent("Done.")];
    }

    private AIContent Call(ScriptedCall call, int index) => new FunctionCallContent($"call-{_turn}-{index + 1}", call.Name, Arguments(call.Arguments));

    private static bool SawUntrustedText(IEnumerable<ChatMessage> messages) => messages
        .SelectMany(message => message.Contents)
        .OfType<FunctionResultContent>()
        .Any(result => result.Result?.ToString()?.Contains("untrusted_text", StringComparison.Ordinal) == true);

    private static Dictionary<string, object?>? Arguments(JsonElement? arguments) =>
        arguments is { ValueKind: JsonValueKind.Object } value
            ? value.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone())
            : null;
}
