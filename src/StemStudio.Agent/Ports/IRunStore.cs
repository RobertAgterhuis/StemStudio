using StemStudio.Agent.Model;

namespace StemStudio.Agent.Ports;

/// <summary>Runs, steps and approvals (data-model 4.1-4.3). Implemented by the backend (T021).</summary>
public interface IRunStore
{
    /// <summary>Stores a new run; fails with <see cref="InvalidOperationException"/> when another run is active (one globally).</summary>
    /// <param name="run">The run (status queued).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task CreateRunAsync(AgentRun run, CancellationToken cancellationToken);

    /// <summary>Loads a run.</summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The run, or null.</returns>
    Task<AgentRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Replaces the run's mutable fields.</summary>
    /// <param name="run">The changed run.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task UpdateRunAsync(AgentRun run, CancellationToken cancellationToken);

    /// <summary>The non-terminal run, when any (at most one globally).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The active run, or null.</returns>
    Task<AgentRun?> GetActiveRunAsync(CancellationToken cancellationToken);

    /// <summary>Inserts or replaces a step.</summary>
    /// <param name="runStep">The step.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveStepAsync(RunStep runStep, CancellationToken cancellationToken);

    /// <summary>Steps of a run in Seq order.</summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The steps.</returns>
    Task<IReadOnlyList<RunStep>> GetStepsAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces an approval.</summary>
    /// <param name="approval">The approval.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveApprovalAsync(Approval approval, CancellationToken cancellationToken);

    /// <summary>Loads the approval of a step.</summary>
    /// <param name="stepId">Step identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The approval, or null.</returns>
    Task<Approval?> GetApprovalAsync(Guid stepId, CancellationToken cancellationToken);
}
