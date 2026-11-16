using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>
/// <c>POST /api/agent/runs/{runId}/approvals/{stepId}</c> (FR-015): validates the body and the ids, then lets the agent library
/// decide (hash re-check, idempotency, <c>approval.resolved</c> to every subscriber through the event hub).
/// </summary>
/// <param name="runs">Run store.</param>
/// <param name="lifecycle">Run end bookkeeping.</param>
/// <param name="agent">Run service of the agent library.</param>
public sealed class AgentApprovalService(IRunStore runs, AgentRunLifecycle lifecycle, IAgentRunService agent)
{
    /// <summary>Records a decision.</summary>
    /// <param name="runId">Run id.</param>
    /// <param name="stepId">Step id.</param>
    /// <param name="request">Decision, scope and the hash shown on the card.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400, 404 or 409.</returns>
    public async Task<IResult> DecideAsync(Guid runId, Guid stepId, DecideApprovalRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || !IsHash(request.ArgumentsHash))
        {
            return ApiProblems.Validation("argumentsHash", "argumentsHash must be the 64-character SHA-256 hex shown on the card.");
        }

        if (await runs.GetApprovalAsync(stepId, cancellationToken) is not { } approval || approval.RunId != runId)
        {
            return ApiProblems.NotFound("The run has no approval for this step.");
        }

        return await DecideKnownAsync(runId, stepId, request, cancellationToken);
    }

    private async Task<IResult> DecideKnownAsync(Guid runId, Guid stepId, DecideApprovalRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var state = await agent.DecideAsync(runId, stepId, request, cancellationToken);
            await lifecycle.ReleaseIfEndedAsync(runId, cancellationToken);
            return TypedResults.Ok(state);
        }
        catch (AgentConflictException conflict)
        {
            return AgentProblems.Conflict(conflict.ProblemCode, conflict.Message);
        }
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f');
}
