using StemStudio.Agent.Runtime;

namespace StemStudio.Agent.Backends.Fakes;

/// <summary>
/// The chat session of the E2E fake stack (<c>STEMSTUDIO_AGENT_BACKEND=fake</c>): every run plays the script from its first
/// turn, with <c>{{context.trackId}}</c> and <c>{{context.mixId}}</c> replaced by the page context of the run's command
/// (server-generated ids; empty when absent). One run at a time, like the API (one active run globally).
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

    private string Substitute(ChatTurnContext context) => scriptJson
        .Replace(TrackPlaceholder, context.Command.TrackId?.ToString() ?? string.Empty, StringComparison.Ordinal)
        .Replace(MixPlaceholder, context.Command.MixId?.ToString() ?? string.Empty, StringComparison.Ordinal);
}
