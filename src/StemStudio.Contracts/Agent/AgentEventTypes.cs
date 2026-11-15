namespace StemStudio.Contracts.Agent;

/// <summary>SSE <c>event:</c> names of GET /api/agent/runs/{runId}/events (contracts/DELTA.md section 3).</summary>
public static class AgentEventTypes
{
    /// <summary>Data: <see cref="RunStartedEventDto"/>.</summary>
    public const string RunStarted = "run.started";

    /// <summary>Data: <see cref="ModelChangedEventDto"/>.</summary>
    public const string ModelChanged = "model.changed";

    /// <summary>Data: <see cref="PlanUpdatedEventDto"/>.</summary>
    public const string PlanUpdated = "plan.updated";

    /// <summary>Data: <see cref="TextDeltaEventDto"/>.</summary>
    public const string TextDelta = "text.delta";

    /// <summary>Data: <see cref="AgentStepDto"/>.</summary>
    public const string StepCreated = "step.created";

    /// <summary>Data: <see cref="StepUpdatedEventDto"/>.</summary>
    public const string StepUpdated = "step.updated";

    /// <summary>Data: <see cref="ApprovalRequestDto"/>.</summary>
    public const string ApprovalRequested = "approval.requested";

    /// <summary>Data: <see cref="ApprovalStateDto"/>.</summary>
    public const string ApprovalResolved = "approval.resolved";

    /// <summary>Data: <see cref="UiRequestedEventDto"/>.</summary>
    public const string UiRequested = "ui.requested";

    /// <summary>Data: <see cref="TokenUsageDto"/>.</summary>
    public const string UsageUpdated = "usage.updated";

    /// <summary>Data: <see cref="RunCompletedEventDto"/>; terminal.</summary>
    public const string RunCompleted = "run.completed";

    /// <summary>Data: <see cref="RunFailureDto"/>; terminal.</summary>
    public const string RunFailed = "run.failed";

    /// <summary>Data: <see cref="RunCancelledEventDto"/>; terminal.</summary>
    public const string RunCancelled = "run.cancelled";

    /// <summary>Data: <see cref="RunInterruptedEventDto"/>; terminal.</summary>
    public const string RunInterrupted = "run.interrupted";

    /// <summary>Every event type, in the order of the contract table.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        RunStarted, ModelChanged, PlanUpdated, TextDelta, StepCreated, StepUpdated, ApprovalRequested,
        ApprovalResolved, UiRequested, UsageUpdated, RunCompleted, RunFailed, RunCancelled, RunInterrupted,
    ];

    /// <summary>True for the events after which the stream closes.</summary>
    /// <param name="eventType">An event type.</param>
    /// <returns>True for run.completed, run.failed, run.cancelled and run.interrupted.</returns>
    public static bool IsTerminal(string eventType) =>
        eventType is RunCompleted or RunFailed or RunCancelled or RunInterrupted;
}
