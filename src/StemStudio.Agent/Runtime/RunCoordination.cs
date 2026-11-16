using System.Collections.Concurrent;
using StemStudio.Agent.Model;

namespace StemStudio.Agent.Runtime;

/// <summary>In-memory state shared by the runner and the inbound service: pending approvals and active runs.</summary>
internal sealed class RunCoordination
{
    /// <summary>Approvals the runner waits for.</summary>
    public ApprovalWaiter Approvals { get; } = new();

    /// <summary>Cancellation of active runs.</summary>
    public RunControls Controls { get; } = new();
}

/// <summary>
/// The runner's in-memory wait for a decision (plan: "in-memory approval wait"). The approval itself is persisted; after an
/// API restart nothing waits any more and the run is interrupted by the backend.
/// </summary>
internal sealed class ApprovalWaiter
{
    private readonly ConcurrentDictionary<Guid, (TaskCompletionSource<Approval> Signal, bool AllowPlanScope)> _pending = new();

    /// <summary>True while the runner waits for the step's decision.</summary>
    public bool IsWaiting(Guid stepId) => _pending.ContainsKey(stepId);

    /// <summary>True when the card allowed a plan-scoped approval.</summary>
    public bool AllowsPlanScope(Guid stepId) => _pending.TryGetValue(stepId, out var entry) && entry.AllowPlanScope;

    /// <summary>Waits for the decision; null when the lifetime passed first.</summary>
    public async Task<Approval?> WaitAsync(Guid stepId, bool allowPlanScope, TimeProvider time, CancellationToken cancellationToken)
    {
        var signal = new TaskCompletionSource<Approval>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[stepId] = (signal, allowPlanScope);
        try
        {
            return await RaceAsync(signal.Task, time, cancellationToken);
        }
        finally
        {
            _pending.TryRemove(stepId, out _);
        }
    }

    private static async Task<Approval?> RaceAsync(Task<Approval> decision, TimeProvider time, CancellationToken cancellationToken)
    {
        using var expiry = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var expired = Task.Delay(Approval.Lifetime, time, expiry.Token);
        var first = await Task.WhenAny(decision, expired);
        await expiry.CancelAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return first == decision ? await decision : null;
    }

    /// <summary>Hands the decision to the waiting runner.</summary>
    public bool Complete(Approval approval) => _pending.TryGetValue(approval.Id, out var entry) && entry.Signal.TrySetResult(approval);
}

/// <summary>Cancellation handles of the runs executing in this process.</summary>
internal sealed class RunControls
{
    private readonly ConcurrentDictionary<Guid, RunControl> _active = new();

    /// <summary>Registers a run; dispose the handle when the run ends.</summary>
    public RunControl Register(Guid runId, CancellationToken cancellationToken)
    {
        var control = new RunControl(runId, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken), this);
        _active[runId] = control;
        return control;
    }

    /// <summary>Cancels an executing run; false when it does not execute here.</summary>
    public bool Cancel(Guid runId, bool cancelJobs)
    {
        if (!_active.TryGetValue(runId, out var control))
        {
            return false;
        }

        control.CancelJobs = cancelJobs;
        control.Source.Cancel();
        return true;
    }

    /// <summary>Removes a run.</summary>
    public void Remove(Guid runId) => _active.TryRemove(runId, out _);
}

/// <summary>The cancellation handle of one executing run.</summary>
/// <param name="RunId">Run identifier.</param>
/// <param name="Source">Cancellation source linked to the host's token.</param>
/// <param name="Owner">Registry.</param>
internal sealed record RunControl(Guid RunId, CancellationTokenSource Source, RunControls Owner) : IDisposable
{
    /// <summary>True when the user asked to cancel the run's jobs as well.</summary>
    public bool CancelJobs { get; set; }

    /// <summary>The run's token.</summary>
    public CancellationToken Token => Source.Token;

    /// <inheritdoc />
    public void Dispose()
    {
        Owner.Remove(RunId);
        Source.Dispose();
    }
}
