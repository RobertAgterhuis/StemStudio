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
    /// <param name="run">The run (backend, usage so far).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The turn's client.</returns>
    Task<ChatTurn> NextTurnAsync(AgentRun run, CancellationToken cancellationToken);
}

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
