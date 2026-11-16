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
    /// <summary>Starts the undo run.</summary>
    /// <param name="conversationId">Conversation id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404 or 409.</returns>
    public async Task<IResult> UndoAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (await conversations.GetAsync(conversationId, cancellationToken) is null)
        {
            return AgentConversationService.ConversationNotFound();
        }

        if (await runs.GetActiveRunAsync(cancellationToken) is not null)
        {
            return AgentProblems.RunActive();
        }

        Guid runId;
        try
        {
            runId = await agent.UndoAsync(conversationId, cancellationToken);
        }
        catch (AgentConflictException conflict)
        {
            return AgentProblems.Conflict(conflict.ProblemCode, conflict.Message);
        }
        catch (KeyNotFoundException)
        {
            return AgentConversationService.ConversationNotFound();
        }

        // Re-read: the agent library may have touched the conversation while it planned the undo run.
        if (await conversations.GetAsync(conversationId, cancellationToken) is { } conversation)
        {
            await conversations.UpdateAsync(conversation with { ActiveRunId = runId }, cancellationToken);
        }

        executor.Enqueue(runId);
        return TypedResults.Accepted((string?)null, new AgentRunAcceptedDto { RunId = runId });
    }
}
