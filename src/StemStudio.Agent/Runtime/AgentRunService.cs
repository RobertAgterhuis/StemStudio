using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>
/// The inbound port the backend calls (<see cref="IAgentRunService"/>): executes runs, records approval decisions for the
/// waiting runner (first decision wins, bound to the hash on the card) and cancels runs. One instance per process.
/// </summary>
public sealed class AgentRunService : IAgentRunService
{
    private readonly AgentServices _services;
    private readonly RunCoordination _coordination = new();
    private readonly AgentRunner _runner;
    private readonly RunEvents _events;

    /// <summary>Creates the service.</summary>
    /// <param name="services">Ports and services.</param>
    public AgentRunService(AgentServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _runner = new AgentRunner(services, _coordination);
        _events = new RunEvents(services.Events);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(Guid runId, CancellationToken cancellationToken) => _runner.ExecuteAsync(runId, cancellationToken);

    /// <inheritdoc />
    public async Task<ApprovalStateDto> DecideAsync(Guid runId, Guid stepId, DecideApprovalRequest decision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var approval = await _services.Runs.GetApprovalAsync(stepId, cancellationToken);
        if (approval is null || approval.RunId != runId)
        {
            throw new KeyNotFoundException("The approval does not exist.");
        }

        if (approval.Status is ApprovalStatusDto.Approved or ApprovalStatusDto.Rejected)
        {
            return ApprovalDecisions.State(approval);
        }

        CheckDecidable(approval, decision);
        var decided = Decided(approval, decision);
        await _services.Runs.SaveApprovalAsync(decided, cancellationToken);
        await _events.PublishAsync(runId, AgentEventTypes.ApprovalResolved, ApprovalDecisions.State(decided), cancellationToken);
        _coordination.Approvals.Complete(decided);
        return ApprovalDecisions.State(decided);
    }

    /// <inheritdoc />
    public async Task CancelAsync(Guid runId, bool cancelJobs, CancellationToken cancellationToken)
    {
        var run = await _services.Runs.GetRunAsync(runId, cancellationToken) ?? throw new KeyNotFoundException("The run does not exist.");
        if (run.IsTerminal)
        {
            throw new AgentConflictException(AgentProblemTypes.RunEnded, "The run has already ended.");
        }

        if (!_coordination.Controls.Cancel(runId, cancelJobs))
        {
            await CancelIdleAsync(run, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<Guid> UndoAsync(Guid conversationId, CancellationToken cancellationToken) =>
        throw new AgentConflictException(AgentProblemTypes.NothingToUndo, "There is nothing to undo.");

    private void CheckDecidable(Approval approval, DecideApprovalRequest decision)
    {
        if (approval.Status != ApprovalStatusDto.Pending || approval.IsExpiredAt(_services.Time.GetUtcNow()) || !_coordination.Approvals.IsWaiting(approval.Id))
        {
            throw new AgentConflictException(AgentProblemTypes.ApprovalNotPending, "The approval is no longer pending.");
        }

        if (!string.Equals(approval.ArgumentsHash, decision.ArgumentsHash, StringComparison.Ordinal))
        {
            throw new AgentConflictException(AgentProblemTypes.ArgumentsChanged, "The arguments changed since the card was shown.");
        }
    }

    private Approval Decided(Approval approval, DecideApprovalRequest decision)
    {
        var approve = decision.Decision == ApprovalDecisionDto.Approve;
        var planScope = approve && decision.Scope == ApprovalScopeDto.PlanHeavy && _coordination.Approvals.AllowsPlanScope(approval.Id);
        return approval with
        {
            Status = approve ? ApprovalStatusDto.Approved : ApprovalStatusDto.Rejected,
            Scope = planScope ? ApprovalScopeDto.PlanHeavy : ApprovalScopeDto.Once,
            DecidedAt = _services.Time.GetUtcNow(),
        };
    }

    private async Task CancelIdleAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var now = _services.Time.GetUtcNow();
        await _services.Runs.UpdateRunAsync(run with { Status = AgentRunStatusDto.Cancelled, EndedAt = now }, cancellationToken);
        var conversation = await _services.Conversations.GetAsync(run.ConversationId, cancellationToken);
        if (conversation is not null && conversation.ActiveRunId == run.Id)
        {
            await _services.Conversations.UpdateAsync(conversation with { ActiveRunId = null, UpdatedAt = now }, cancellationToken);
        }

        await _events.PublishAsync(run.Id, AgentEventTypes.RunCancelled, new RunCancelledEventDto { RunId = run.Id }, cancellationToken);
    }
}
