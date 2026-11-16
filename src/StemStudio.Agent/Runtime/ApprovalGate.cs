using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>What became of an approval request.</summary>
internal enum ApprovalVerdict
{
    /// <summary>Approved with the identical arguments hash.</summary>
    Approved,

    /// <summary>Rejected by the user (or approved for other arguments).</summary>
    Rejected,

    /// <summary>Not decided within 24 h.</summary>
    Expired,
}

/// <summary>
/// Suspends a step until the user decides (FR-012): persists the pending approval, publishes the card, waits in memory,
/// re-checks the hash binding (FR-015) and records a plan-scoped grant bound to the tool and its single target.
/// </summary>
/// <param name="services">Ports.</param>
/// <param name="events">Event publisher.</param>
/// <param name="waiter">In-memory wait.</param>
internal sealed class ApprovalGate(AgentServices services, RunEvents events, ApprovalWaiter waiter)
{
    /// <summary>Asks the user and returns the verdict with the updated step.</summary>
    public async Task<(ApprovalVerdict Verdict, RunStep Step)> AskAsync(RunState state, RunStep step, PreparedCall prepared, CancellationToken cancellationToken)
    {
        var estimate = await EstimateAsync(state, prepared, cancellationToken);
        step = step with { EstimatedMinutes = estimate?.EstimatedMinutes };
        var approval = new Approval { Id = step.Id, RunId = state.RunId, Status = ApprovalStatusDto.Pending, Scope = ApprovalScopeDto.Once, ArgumentsHash = step.ArgumentsHash, RequestedAt = services.Time.GetUtcNow() };
        await services.Runs.SaveStepAsync(step, cancellationToken);
        await services.Runs.SaveApprovalAsync(approval, cancellationToken);
        await SetRunStatusAsync(state, AgentRunStatusDto.AwaitingApproval, cancellationToken);
        var allowPlanScope = AllowsPlanScope(state, prepared);
        using var wait = waiter.Register(approval.Id, allowPlanScope);
        await events.PublishAsync(state.RunId, AgentEventTypes.ApprovalRequested, Card(step, prepared, estimate, allowPlanScope), cancellationToken);
        var decided = await WaitAsync(state, approval, wait, cancellationToken);
        await SetRunStatusAsync(state, AgentRunStatusDto.Running, cancellationToken);
        return (Verdict(state, prepared, decided), step);
    }

    private async Task<Approval?> WaitAsync(RunState state, Approval approval, ApprovalWait wait, CancellationToken cancellationToken)
    {
        try
        {
            return await wait.WaitAsync(services.Time, cancellationToken) ?? await ExpireAsync(state, approval, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            await ExpireAsync(state, approval, CancellationToken.None);
            throw;
        }
    }

    private async Task<Approval?> ExpireAsync(RunState state, Approval approval, CancellationToken cancellationToken)
    {
        var expired = approval with { Status = ApprovalStatusDto.Expired, DecidedAt = services.Time.GetUtcNow() };
        await services.Runs.SaveApprovalAsync(expired, cancellationToken);
        await events.PublishAsync(state.RunId, AgentEventTypes.ApprovalResolved, ApprovalDecisions.State(expired), cancellationToken);
        return null;
    }

    private static ApprovalVerdict Verdict(RunState state, PreparedCall prepared, Approval? decided)
    {
        if (decided is null || !Policy.ApprovalBinding.Matches(decided, prepared.Validation.Hash))
        {
            return decided is null ? ApprovalVerdict.Expired : ApprovalVerdict.Rejected;
        }

        if (Policy.ApprovalBinding.Grant(decided, prepared.Tool!.Name, SingleTarget(prepared)) is { } grant)
        {
            state.Grants.Add(grant);
        }

        return ApprovalVerdict.Approved;
    }

    private async Task<ToolEstimate?> EstimateAsync(RunState state, PreparedCall prepared, CancellationToken cancellationToken)
    {
        if (prepared.Tool!.Estimator is not { } estimator)
        {
            return null;
        }

        try
        {
            return await estimator(Invocation(state, prepared), cancellationToken);
        }
        catch (AgentActionException)
        {
            return null;
        }
    }

    private ToolInvocation Invocation(RunState state, PreparedCall prepared) =>
        new() { Arguments = prepared.Validation.Arguments, Actions = services.Actions, Context = state.Context, Backend = state.Run.Backend };

    private static bool AllowsPlanScope(RunState state, PreparedCall prepared) =>
        prepared.Tool!.Risk == ToolRiskDto.Heavy && state.Run.Plan.Any(item => item.Tool == prepared.Tool.Name) && SingleTarget(prepared) is not null;

    private static string? SingleTarget(PreparedCall prepared) =>
        prepared.Tool!.TargetArgument is { } name && prepared.Validation.Arguments.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    private static ApprovalRequestDto Card(RunStep step, PreparedCall prepared, ToolEstimate? estimate, bool allowPlanScope) => new()
    {
        StepId = step.Id,
        Tool = step.Tool,
        Risk = step.Risk,
        Arguments = prepared.Validation.Arguments,
        ArgumentsHash = step.ArgumentsHash,
        Reasons = step.DecisionReasons,
        Effect = estimate?.Effect ?? prepared.Tool!.Title,
        Undoable = step.UndoKind != UndoKindDto.None,
        EstimatedMinutes = estimate?.EstimatedMinutes,
        Device = estimate?.DeviceCode,
        AllowPlanScope = allowPlanScope,
    };

    private async Task SetRunStatusAsync(RunState state, AgentRunStatusDto status, CancellationToken cancellationToken)
    {
        state.Run = state.Run with { Status = status };
        await services.Runs.UpdateRunAsync(state.Run, cancellationToken);
    }
}

/// <summary>Contract views of approval decisions.</summary>
internal static class ApprovalDecisions
{
    /// <summary>The ApprovalState of an approval.</summary>
    public static ApprovalStateDto State(Approval approval) =>
        new() { StepId = approval.Id, Status = approval.Status, Scope = approval.Scope, DecidedAt = approval.DecidedAt };
}
