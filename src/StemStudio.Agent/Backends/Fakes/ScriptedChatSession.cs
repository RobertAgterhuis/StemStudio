using System.Text.Json.Nodes;
using StemStudio.Agent.Runtime;

namespace StemStudio.Agent.Backends.Fakes;

/// <summary>
/// The chat session of the E2E fake stack (<c>STEMSTUDIO_AGENT_BACKEND=fake</c>): every run plays the script from its first
/// turn. A call argument string (also inside argument lists and records) that is exactly <c>{{context.trackId}}</c> or
/// <c>{{context.mixId}}</c> is replaced by the page context of the run's command (server-generated ids; empty when absent);
/// any other <c>{{...}}</c> text, including a placeholder inside a longer string or in assistant text, is passed through
/// unchanged (contracts/DELTA.md section 8). One run at a time, like the API (one active run globally).
/// </summary>
/// <param name="scriptJson">Script file contents (contracts/agent-script.schema.json).</param>
public sealed class ScriptedChatSession(string scriptJson) : IAgentChatSession
{
    /// <summary>Placeholder for the command's track id.</summary>
    public const string TrackPlaceholder = "{{context.trackId}}";

    /// <summary>Placeholder for the command's mix id.</summary>
    public const string MixPlaceholder = "{{context.mixId}}";

    private readonly Lock _gate = new();
    private (Guid RunId, ScriptedChatClient Client)? _current;

    /// <inheritdoc />
    public Task<ChatTurn> NextTurnAsync(ChatTurnContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate)
        {
            if (_current is not { } current || current.RunId != context.Run.Id)
            {
                _current = (context.Run.Id, ScriptedChatClient.FromJson(Substitute(context)));
            }

            return Task.FromResult(new ChatTurn(_current.Value.Client, ScriptedChatClient.ModelId, false));
        }
    }

    private string Substitute(ChatTurnContext context)
    {
        if (JsonNode.Parse(scriptJson) is not JsonObject script || script["turns"] is not JsonArray turns)
        {
            return scriptJson;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TrackPlaceholder] = context.Command.TrackId?.ToString() ?? string.Empty,
            [MixPlaceholder] = context.Command.MixId?.ToString() ?? string.Empty,
        };
        foreach (var call in turns.OfType<JsonObject>().Select(turn => turn["calls"]).OfType<JsonArray>().SelectMany(calls => calls.OfType<JsonObject>()))
        {
            call["arguments"] = Replace(call["arguments"], values);
        }

        return script.ToJsonString();
    }

    private static JsonNode? Replace(JsonNode? node, Dictionary<string, string> values) => node switch
    {
        JsonObject record => Rebuild(record, values),
        JsonArray list => new JsonArray([.. list.Select(item => Replace(item, values))]),
        JsonValue value when value.TryGetValue<string>(out var text) && values.TryGetValue(text, out var id) => JsonValue.Create(id),
        _ => node?.DeepClone(),
    };

    private static JsonObject Rebuild(JsonObject record, Dictionary<string, string> values)
    {
        var copy = new JsonObject();
        foreach (var (name, value) in record)
        {
            copy[name] = Replace(value, values);
        }

        return copy;
    }
}
