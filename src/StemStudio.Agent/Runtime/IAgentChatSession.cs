using Microsoft.Extensions.AI;
using StemStudio.Agent.Model;

namespace StemStudio.Agent.Runtime;

/// <summary>
/// The chat client for the next model turn of a run, asked once per turn so the model can change between turns (Ollama
/// fallback while a GPU job runs, FR-021); a change is published as <c>model.changed</c>.
/// </summary>
public interface IAgentChatSession
{
    /// <summary>Selects the client and model of the next turn.</summary>
    /// <param name="context">The run and the page context of its command.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The turn's client.</returns>
    Task<ChatTurn> NextTurnAsync(ChatTurnContext context, CancellationToken cancellationToken);
}

/// <summary>What a chat session gets per turn.</summary>
/// <param name="Run">The run (id, backend, usage so far).</param>
/// <param name="Command">Page context of the message that started the run.</param>
public sealed record ChatTurnContext(AgentRun Run, StemStudio.Contracts.Agent.PageContextDto Command);

/// <summary>The client of one model turn.</summary>
/// <param name="Client">Chat client.</param>
/// <param name="Model">Model id shown in the badge.</param>
/// <param name="UsingFallback">True for the Ollama CPU fallback model.</param>
public sealed record ChatTurn(IChatClient Client, string Model, bool UsingFallback)
{
    /// <summary>Readable reason of a model change (e.g. "A GPU job started"), when any.</summary>
    public string? ChangeReason { get; init; }

    /// <summary>Backend options (Ollama num_ctx, think, keep_alive; Claude effort); the runner adds tools and tool mode.</summary>
    public ChatOptions? Options { get; init; }

    /// <summary>Seconds one turn may take before the run fails with <c>timeout</c>; null uses the local limit.</summary>
    public int? TimeoutSeconds { get; init; }
}
