using Microsoft.Extensions.AI;
using StemStudio.Agent.Runtime;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>A chat session that always uses one client; the model per turn can be scripted (model.changed tests).</summary>
/// <param name="client">The chat client.</param>
public sealed class FixedChatSession(IChatClient client) : IAgentChatSession
{
    private int _turn;

    /// <summary>Model per turn (index = turn - 1); the last entry repeats. Default: "scripted".</summary>
    public IReadOnlyList<ChatTurnModel> Models { get; init; } = [new("scripted", false, null)];

    public Task<ChatTurn> NextTurnAsync(ChatTurnContext context, CancellationToken cancellationToken)
    {
        var model = Models[Math.Min(_turn++, Models.Count - 1)];
        return Task.FromResult(new ChatTurn(client, model.Model, model.UsingFallback) { ChangeReason = model.Reason });
    }
}

/// <summary>A scripted model choice.</summary>
public sealed record ChatTurnModel(string Model, bool UsingFallback, string? Reason);
