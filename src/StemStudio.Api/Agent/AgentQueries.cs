using Microsoft.EntityFrameworkCore;
using StemStudio.Agent.Model;
using StemStudio.Api.Agent.Stores;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>Read models of the agent endpoints that the ports do not cover (runs per conversation, pending approval card).</summary>
/// <param name="db">Context per operation.</param>
/// <param name="events">Event log (the approval card is the payload of <c>approval.requested</c>).</param>
public sealed class AgentQueries(AgentDb db, IRunEventLogReader events)
{
    /// <summary>Runs of a conversation, oldest first.</summary>
    /// <param name="conversationId">Conversation id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The runs.</returns>
    public Task<IReadOnlyList<AgentRun>> RunsOfAsync(Guid conversationId, CancellationToken cancellationToken) =>
        db.WithAsync<IReadOnlyList<AgentRun>>(async context =>
        {
            var rows = await context.AgentRuns.AsNoTracking().Where(r => r.ConversationId == conversationId)
                .OrderBy(r => r.StartedAt).ToListAsync(cancellationToken);
            return [.. rows.Select(RunMapping.ToRecord)];
        });

    /// <summary>Status of the newest run per conversation.</summary>
    /// <param name="conversationIds">Conversation ids.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Status by conversation id (absent when the conversation has no run).</returns>
    public Task<Dictionary<Guid, AgentRunStatusDto>> LastRunStatusesAsync(IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken) =>
        db.WithAsync(async context =>
        {
            var rows = await context.AgentRuns.AsNoTracking().Where(r => conversationIds.Contains(r.ConversationId))
                .Select(r => new { r.ConversationId, r.StartedAt, r.Status }).ToListAsync(cancellationToken);
            return rows.GroupBy(r => r.ConversationId)
                .ToDictionary(g => g.Key, g => AgentStoreValues.Parse<AgentRunStatusDto>(g.MaxBy(r => r.StartedAt)!.Status));
        });

    /// <summary>The card of the pending approval of a run: the newest <c>approval.requested</c> payload of a pending step.</summary>
    /// <param name="runId">Run id.</param>
    /// <param name="pendingStepIds">Steps whose approval is pending.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The card, or null.</returns>
    public async Task<ApprovalRequestDto?> PendingApprovalAsync(Guid runId, IReadOnlySet<Guid> pendingStepIds, CancellationToken cancellationToken)
    {
        if (pendingStepIds.Count == 0)
        {
            return null;
        }

        var requested = (await events.ReadAfterAsync(runId, 0, cancellationToken))
            .Where(e => e.Type == AgentEventTypes.ApprovalRequested)
            .Select(e => AgentStoreValues.FromJson<ApprovalRequestDto>(e.Payload));
        return requested.LastOrDefault(card => pendingStepIds.Contains(card.StepId));
    }
}
