using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>
/// <c>POST /api/agent/conversations/{id}/undo</c> (FR-017): the agent library plans the undo run from the newest available undo
/// group (<see cref="IAgentRunService.UndoAsync"/>); the backend marks it as the conversation's active run and queues it.
/// </summary>
/// <param name="conversations">Conversation store.</param>
/// <param name="runs">Run store (one active run globally).</param>
/// <param name="executor">Run executor.</param>
/// <param name="agent">Run service of the agent library.</param>
public sealed class AgentUndoService(IConversationStore conversations, IRunStore runs, AgentRunExecutor executor, IAgentRunService agent)
{
    private const string NotFoundDetail = "The conversation does not exist.";

    /// <summary>Starts the undo run.</summary>
    /// <param name="conversationId">Conversation id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404 or 409.</returns>
    public async Task<IResult> UndoAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (await RefusalAsync(conversationId, cancellationToken) is { } refused)
        {
            return refused;
        }

        var planned = await AgentProblems.CallAsync(() => agent.UndoAsync(conversationId, cancellationToken), NotFoundDetail);
        if (planned.Problem is { } problem)
        {
            return problem;
        }

        await QueueAsync(conversationId, planned.Value, cancellationToken);
        return TypedResults.Accepted((string?)null, new AgentRunAcceptedDto { RunId = planned.Value });
    }

    private async Task<IResult?> RefusalAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (await conversations.GetAsync(conversationId, cancellationToken) is null)
        {
            return AgentConversationService.ConversationNotFound();
        }

        return await runs.GetActiveRunAsync(cancellationToken) is null ? null : AgentProblems.RunActive();
    }

    // Re-read: the agent library may have touched the conversation while it planned the undo run.
    private async Task QueueAsync(Guid conversationId, Guid runId, CancellationToken cancellationToken)
    {
        if (await conversations.GetAsync(conversationId, cancellationToken) is { } conversation)
        {
            await conversations.UpdateAsync(conversation with { ActiveRunId = runId }, cancellationToken);
        }

        executor.Enqueue(runId);
    }
}
