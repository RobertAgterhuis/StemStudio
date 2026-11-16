using StemStudio.Contracts.Agent;

namespace StemStudio.Web.Agent;

/// <summary>
/// What the panel shows of one run: backend and model, plan, steps, streamed text, the pending approval, usage and
/// the failure. Immutable: <see cref="Apply"/> returns the view after an event (DELTA.md section 3), so a render
/// never sees a half-applied event.
/// </summary>
public sealed record AgentRunView
{
    private static readonly Dictionary<string, Func<AgentRunView, AgentEvent, AgentRunView>> Handlers = new(StringComparer.Ordinal)
    {
        [AgentEventTypes.RunStarted] = (view, e) => view.Started(e.Read<RunStartedEventDto>()),
        [AgentEventTypes.ModelChanged] = (view, e) => view.ModelChanged(e.Read<ModelChangedEventDto>()),
        [AgentEventTypes.PlanUpdated] = (view, e) => view with { Plan = e.Read<PlanUpdatedEventDto>().Plan },
        [AgentEventTypes.TextDelta] = (view, e) => view with { Text = view.Text + e.Read<TextDeltaEventDto>().Text },
        [AgentEventTypes.StepCreated] = (view, e) => view.WithStep(e.Read<AgentStepDto>()),
        [AgentEventTypes.StepUpdated] = (view, e) => view.UpdateStep(e.Read<StepUpdatedEventDto>()),
        [AgentEventTypes.ApprovalRequested] = (view, e) => view with { Approval = e.Read<ApprovalRequestDto>() },
        [AgentEventTypes.ApprovalResolved] = (view, e) => view.Resolved(e.Read<ApprovalStateDto>().StepId),
        [AgentEventTypes.UsageUpdated] = (view, e) => view with { Usage = e.Read<TokenUsageDto>() },
        [AgentEventTypes.RunFailed] = (view, e) => view with { Failure = e.Read<RunFailureDto>(), Ended = true, Approval = null },
        [AgentEventTypes.RunCompleted] = (view, _) => view.End(),
        [AgentEventTypes.RunCancelled] = (view, _) => view.End(),
        [AgentEventTypes.RunInterrupted] = (view, _) => view.End() with { Interrupted = true },
    };

    /// <summary>The run.</summary>
    public required Guid RunId { get; init; }

    /// <summary>The backend of the run, once started.</summary>
    public AgentBackendDto? Backend { get; init; }

    /// <summary>The model of the current turn, once started.</summary>
    public string? Model { get; init; }

    /// <summary>True while the Ollama fallback model is used.</summary>
    public bool UsingFallback { get; init; }

    /// <summary>Why the model changed (<c>model.changed</c>), or null.</summary>
    public string? ModelReason { get; init; }

    /// <summary>The plan checklist.</summary>
    public IReadOnlyList<PlanItemDto> Plan { get; init; } = [];

    /// <summary>The steps in order.</summary>
    public IReadOnlyList<AgentStepDto> Steps { get; init; } = [];

    /// <summary>The answer text streamed so far.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>The step waiting for the user's decision, or null.</summary>
    public ApprovalRequestDto? Approval { get; init; }

    /// <summary>Token usage (Claude), or null.</summary>
    public TokenUsageDto? Usage { get; init; }

    /// <summary>The failure of a failed run, or null.</summary>
    public RunFailureDto? Failure { get; init; }

    /// <summary>True after the terminal event.</summary>
    public bool Ended { get; init; }

    /// <summary>True when the API restarted during the run (<c>run.interrupted</c>).</summary>
    public bool Interrupted { get; init; }

    /// <summary>The highest event id applied (resume point).</summary>
    public int LastEventId { get; init; }

    /// <summary>A run that was just accepted.</summary>
    /// <param name="runId">The run.</param>
    /// <returns>The empty view.</returns>
    public static AgentRunView Start(Guid runId) => new() { RunId = runId };

    /// <summary>The view of a stored run snapshot (<c>GET /api/agent/runs/{id}</c>).</summary>
    /// <param name="run">The snapshot.</param>
    /// <returns>The view.</returns>
    public static AgentRunView FromSnapshot(AgentRunDto run) => new()
    {
        RunId = run.RunId,
        Backend = run.Backend,
        Model = run.Model,
        Plan = run.Plan,
        Steps = run.Steps,
        Approval = run.PendingApproval,
        Usage = run.Usage,
        Failure = run.Failure,
        Ended = run.Status is AgentRunStatusDto.Completed or AgentRunStatusDto.Failed or AgentRunStatusDto.Cancelled or AgentRunStatusDto.Interrupted,
        Interrupted = run.Status == AgentRunStatusDto.Interrupted,
    };

    /// <summary>Applies one event; unknown types and <c>ui.requested</c> leave the view unchanged.</summary>
    /// <param name="e">The event.</param>
    /// <returns>The new view.</returns>
    public AgentRunView Apply(AgentEvent e) =>
        (Handlers.TryGetValue(e.Type, out var handler) ? handler(this, e) : this) with { LastEventId = Math.Max(LastEventId, e.Id) };

    private AgentRunView Started(RunStartedEventDto started) =>
        this with { Backend = started.Backend, Model = started.Model, UsingFallback = started.UsingFallback };

    private AgentRunView ModelChanged(ModelChangedEventDto changed) =>
        this with { Model = changed.Model, UsingFallback = changed.UsingFallback, ModelReason = changed.Reason };

    private AgentRunView WithStep(AgentStepDto step) =>
        this with { Steps = [.. Steps.Where(s => s.StepId != step.StepId), step] };

    private AgentRunView UpdateStep(StepUpdatedEventDto update) => this with
    {
        Steps = [.. Steps.Select(s => s.StepId == update.StepId ? Updated(s, update) : s)],
    };

    private static AgentStepDto Updated(AgentStepDto step, StepUpdatedEventDto update) => step with
    {
        Status = update.Status,
        Progress = update.Progress ?? step.Progress,
        ResultSummary = update.ResultSummary ?? step.ResultSummary,
        Error = update.Error ?? step.Error,
    };

    private AgentRunView Resolved(Guid stepId) => Approval?.StepId == stepId ? this with { Approval = null } : this;

    private AgentRunView End() => this with { Ended = true, Approval = null };
}
