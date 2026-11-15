using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Ports;

/// <summary>
/// Inbound port the backend calls (endpoints and the hosted AgentRunExecutor). Implemented by the agent library (ai-agent);
/// registered by <c>AddStemStudioAgent()</c>.
/// </summary>
public interface IAgentRunService
{
    /// <summary>Executes a queued run until it completes, fails or suspends (approval, UI result, job wait).</summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="cancellationToken">Cancellation (API shutdown).</param>
    /// <returns>A task.</returns>
    Task ExecuteAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Records the user's decision on a pending approval and resumes the run.</summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="stepId">Step identifier.</param>
    /// <param name="decision">The decision with the arguments hash shown on the card.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The stored approval state (idempotent); throws <see cref="AgentConflictException"/> for 409 cases.</returns>
    Task<ApprovalStateDto> DecideAsync(Guid runId, Guid stepId, DecideApprovalRequest decision, CancellationToken cancellationToken);

    /// <summary>Cancels a run and optionally the jobs it started.</summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="cancelJobs">True to cancel the jobs as well.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task; throws <see cref="AgentConflictException"/> when the run already ended.</returns>
    Task CancelAsync(Guid runId, bool cancelJobs, CancellationToken cancellationToken);

    /// <summary>Creates an undo run for the newest available undo group of a conversation.</summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued run id; throws <see cref="AgentConflictException"/> (nothing-to-undo, run-active).</returns>
    Task<Guid> UndoAsync(Guid conversationId, CancellationToken cancellationToken);
}
