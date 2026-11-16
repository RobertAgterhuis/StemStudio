using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using StemStudio.Agent.Model;
using StemStudio.Agent.Policy;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>
/// Executes one model call: lookup and validation, the policy decision, then the server handler, the UI round trip or the
/// meta tool, with every state change persisted and published (plan.md "Key flows" 2).
/// </summary>
/// <param name="services">Ports.</param>
/// <param name="events">Event publisher.</param>
/// <param name="approvals">In-memory approval wait.</param>
internal sealed class StepExecutor(AgentServices services, RunEvents events, ApprovalWaiter approvals)
{
    /// <summary>What the model sees for a rejected step.</summary>
    public const string RejectedMessage = "The user rejected this step.";

    /// <summary>What the model sees for an expired approval.</summary>
    public const string ExpiredMessage = "The approval expired; the step did not run.";

    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(AgentContractLimits.UiToolTimeoutSeconds);
    private readonly RunPlan _plan = new(services, events);
    private readonly ApprovalGate _gate = new(services, events, approvals);
    private readonly MetaToolRunner _meta = new(services, events, new RunPlan(services, events));

    /// <summary>Executes the call and returns what the model sees.</summary>
    public async Task<FunctionResultContent> ExecuteAsync(RunState state, FunctionCallContent call, IReadOnlyList<AgentToolDescriptor> offered, CancellationToken cancellationToken)
    {
        await CountCallAsync(state, cancellationToken);
        var prepared = PreparedCall.From(call, offered);
        var result = prepared.IsValid ? await DecideAndRunAsync(state, prepared, cancellationToken) : await RejectInvalidAsync(state, prepared, cancellationToken);
        return new FunctionResultContent(call.CallId, result.Json);
    }

    /// <summary>Executes a recorded undo call (trusted arguments from the undo store) and returns the finished step.</summary>
    public async Task<RunStep> ExecuteUndoAsync(RunState state, PreparedCall prepared, CancellationToken cancellationToken)
    {
        await CountCallAsync(state, cancellationToken);
        await DecideAndRunAsync(state, prepared, cancellationToken);
        var steps = await services.Runs.GetStepsAsync(state.RunId, cancellationToken);
        return steps.MaxBy(step => step.Seq)!;
    }

    private async Task CountCallAsync(RunState state, CancellationToken cancellationToken)
    {
        if (state.Run.ToolCalls >= state.Limits.MaxToolCalls)
        {
            throw new RunStopException(AgentFailureCodes.LimitReached, $"The run reached the limit of {state.Limits.MaxToolCalls} tool calls.");
        }

        await SaveRunAsync(state, state.Run with { ToolCalls = state.Run.ToolCalls + 1 }, cancellationToken);
    }

    private async Task<ProjectedResult> RejectInvalidAsync(RunState state, PreparedCall prepared, CancellationToken cancellationToken)
    {
        var step = prepared.ToStep(state.RunId, ++state.StepSeq, AgentStepStatusDto.RejectedInvalid) with { Error = Cap(prepared.Error) };
        await services.Runs.SaveStepAsync(step, cancellationToken);
        await events.StepCreatedAsync(step, cancellationToken);
        await SaveRunAsync(state, state.Run with { InvalidCalls = state.Run.InvalidCalls + 1 }, cancellationToken);
        return ResultProjector.Error(prepared.Error);
    }

    private async Task<ProjectedResult> DecideAndRunAsync(RunState state, PreparedCall call, CancellationToken cancellationToken)
    {
        var prepared = await EscalateAsync(state, call, cancellationToken);
        var decision = ToolPolicy.Decide(PolicyFor(state, prepared));
        var status = decision.Decision switch
        {
            StepDecisionDto.Denied => AgentStepStatusDto.Failed,
            StepDecisionDto.ApprovalRequired => AgentStepStatusDto.AwaitingApproval,
            _ => AgentStepStatusDto.Running,
        };
        var planIndex = await _plan.LinkAsync(state, prepared.Tool!, cancellationToken);
        var step = prepared.ToStep(state.RunId, ++state.StepSeq, status) with { Decision = decision.Decision, DecisionReasons = decision.Reasons, Error = decision.Refusal, PlanIndex = planIndex };
        step = Started(step, status);
        await services.Runs.SaveStepAsync(step, cancellationToken);
        await events.StepCreatedAsync(step, cancellationToken);
        return decision.Decision switch
        {
            StepDecisionDto.Denied => ResultProjector.Error(decision.Refusal!),
            StepDecisionDto.ApprovalRequired => await AskThenRunAsync(state, step, prepared, cancellationToken),
            _ => await RunAsync(state, step, prepared, cancellationToken),
        };
    }

    private async Task<ProjectedResult> AskThenRunAsync(RunState state, RunStep step, PreparedCall prepared, CancellationToken cancellationToken)
    {
        var (verdict, asked) = await AskAsync(state, step, prepared, cancellationToken);
        if (verdict == ApprovalVerdict.Approved)
        {
            return await RunAsync(state, asked with { Status = AgentStepStatusDto.Running, StartedAt = services.Time.GetUtcNow() }, prepared, cancellationToken);
        }

        var (status, message) = verdict == ApprovalVerdict.Rejected ? (AgentStepStatusDto.Rejected, RejectedMessage) : (AgentStepStatusDto.Expired, ExpiredMessage);
        await _plan.SetAsync(state, step.PlanIndex, PlanItemStatusDto.Skipped, cancellationToken);
        return await FinishAsync(asked with { Status = status, Error = message }, null, cancellationToken);
    }

    private async Task<(ApprovalVerdict Verdict, RunStep Step)> AskAsync(RunState state, RunStep step, PreparedCall prepared, CancellationToken cancellationToken)
    {
        try
        {
            return await _gate.AskAsync(state, step, prepared, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await services.Runs.SaveStepAsync(step with { Status = AgentStepStatusDto.Expired, EndedAt = services.Time.GetUtcNow() }, CancellationToken.None);
            throw;
        }
    }

    private async Task<PreparedCall> EscalateAsync(RunState state, PreparedCall prepared, CancellationToken cancellationToken)
    {
        if (prepared.Tool!.HeavyWhen is not { } heavyWhen || prepared.Tool.Risk == ToolRiskDto.Heavy)
        {
            return prepared;
        }

        try
        {
            var invocation = new ToolInvocation { Arguments = prepared.Validation.Arguments, Actions = services.Actions, Context = state.Context, Backend = state.Run.Backend };
            return await heavyWhen(invocation, cancellationToken) ? prepared with { Tool = prepared.Tool with { Risk = ToolRiskDto.Heavy } } : prepared;
        }
        catch (AgentActionException)
        {
            return prepared;
        }
    }

    private RunStep Started(RunStep step, AgentStepStatusDto status) =>
        status == AgentStepStatusDto.Running ? step with { StartedAt = services.Time.GetUtcNow() } : step;

    private static PolicyInput PolicyFor(RunState state, PreparedCall prepared) => new()
    {
        Tool = prepared.Tool!,
        Arguments = prepared.Validation.Arguments,
        Tainted = state.Run.Tainted,
        UserMessages = state.UserMessages,
        Plan = state.Run.Plan,
        PlanGrants = state.Grants,
        IsCompensation = state.CurrentUndo?.Kind == UndoKindDto.Compensate,
        Fresh = state.CurrentUndo?.Fresh == true,
    };

    private async Task<ProjectedResult> RunAsync(RunState state, RunStep step, PreparedCall prepared, CancellationToken cancellationToken)
    {
        ToolResult result;
        try
        {
            result = await InvokeAsync(state, step, prepared, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return await FailStepAsync(state, step, StepFailure.From(exception, services.Logger, state.RunId, step.Tool), cancellationToken);
        }

        return await SucceedAsync(state, step, result, cancellationToken);
    }

    private async Task<ProjectedResult> FailStepAsync(RunState state, RunStep step, StepFailure failure, CancellationToken cancellationToken)
    {
        await FinishAsync(step with { Status = AgentStepStatusDto.Failed, Error = Cap(failure.StepError) }, null, cancellationToken);
        await _plan.SetAsync(state, step.PlanIndex, PlanItemStatusDto.Failed, cancellationToken);
        var result = failure.ModelResult();
        if (result.Tainted && !state.Run.Tainted)
        {
            await SaveRunAsync(state, state.Run with { Tainted = true }, cancellationToken);
        }

        return result;
    }

    private Task<ToolResult> InvokeAsync(RunState state, RunStep step, PreparedCall prepared, CancellationToken cancellationToken) => prepared.Tool!.Kind switch
    {
        ToolKindDto.Ui => RunUiToolAsync(state, step, prepared, cancellationToken),
        ToolKindDto.Meta => _meta.ExecuteAsync(state, step, prepared.Validation.Arguments, cancellationToken),
        _ => prepared.Tool.Handler!(Invocation(state, step, prepared), cancellationToken),
    };

    private ToolInvocation Invocation(RunState state, RunStep step, PreparedCall prepared) => new()
    {
        Arguments = prepared.Validation.Arguments,
        Actions = services.Actions,
        Context = state.Context,
        Backend = state.Run.Backend,
        Ui = (tool, arguments, cancellationToken) => RequestUiAsync(state, step, new UiCall(tool, arguments), cancellationToken),
    };

    private async Task<ToolResult> RunUiToolAsync(RunState state, RunStep step, PreparedCall prepared, CancellationToken cancellationToken)
    {
        var answer = await RequestUiAsync(state, step, new UiCall(step.Tool, step.Arguments), cancellationToken);
        if (!answer.Ok)
        {
            throw new UiToolFailedException(answer.Error ?? StepFailure.PageFailed);
        }

        var element = RunEvents.Element(answer.Result);
        var result = prepared.Tool!.MapUiResult?.Invoke(prepared.Validation.Arguments, element)
            ?? ToolResult.Of(JsonNode.Parse(answer.Result ?? "{}") ?? new JsonObject());
        return WithUiUndo(step, element, result);
    }

    private async Task<UiToolResult> RequestUiAsync(RunState state, RunStep step, UiCall call, CancellationToken cancellationToken)
    {
        var waiting = step with { Status = AgentStepStatusDto.AwaitingUi };
        await services.Runs.SaveStepAsync(waiting, cancellationToken);
        await events.StepUpdatedAsync(waiting, cancellationToken);
        await SaveRunAsync(state, state.Run with { Status = AgentRunStatusDto.AwaitingUi }, cancellationToken);
        var request = new UiToolRequest { RunId = state.RunId, StepId = step.Id, Tool = call.Tool, Arguments = call.Arguments, Timeout = UiTimeout };
        var pending = services.Ui.RequestAsync(request, cancellationToken);
        var payload = new UiRequestedEventDto { StepId = step.Id, Tool = call.Tool, Arguments = RunEvents.Element(call.Arguments)!.Value, TimeoutSeconds = AgentContractLimits.UiToolTimeoutSeconds };
        await events.PublishAsync(state.RunId, AgentEventTypes.UiRequested, payload, cancellationToken);
        var answer = await pending;
        await SaveRunAsync(state, state.Run with { Status = AgentRunStatusDto.Running }, cancellationToken);
        return answer;
    }

    private async Task<ProjectedResult> SucceedAsync(RunState state, RunStep step, ToolResult result, CancellationToken cancellationToken)
    {
        var projected = ResultProjector.Project(result.Projection);
        if (projected.Tainted && !state.Run.Tainted)
        {
            await SaveRunAsync(state, state.Run with { Tainted = true }, cancellationToken);
        }

        PageTransitions.Apply(state, step);
        var done = step with { Status = AgentStepStatusDto.Succeeded, Job = result.Job ?? step.Job, EstimatedMinutes = result.EstimatedMinutes ?? step.EstimatedMinutes, Progress = result.Progress ?? step.Progress };
        await FinishAsync(done, projected.Json, cancellationToken);
        await RecordUndoAsync(UndoRecorder.Entries(state, done, result), cancellationToken);
        await _plan.SetAsync(state, step.PlanIndex, PlanItemStatusDto.Done, cancellationToken);
        return projected;
    }

    private async Task<ProjectedResult> FinishAsync(RunStep step, string? resultJson, CancellationToken cancellationToken)
    {
        var finished = step with { ResultSummary = resultJson, EndedAt = services.Time.GetUtcNow() };
        await services.Runs.SaveStepAsync(finished, cancellationToken);
        await events.StepUpdatedAsync(finished, cancellationToken);
        return resultJson is null ? ResultProjector.Error(step.Error ?? "The step did not run.") : new ProjectedResult(resultJson, false, false);
    }

    private async Task RecordUndoAsync(IReadOnlyList<UndoEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count > 0)
        {
            await services.Undo.AddAsync(entries, cancellationToken);
        }
    }

    private static ToolResult WithUiUndo(RunStep step, JsonElement? page, ToolResult result) =>
        page is { } answer && step.UndoKind != UndoKindDto.None ? result with { MoreUndo = UiUndo.From(step.Tool, answer) } : result;

    private async Task SaveRunAsync(RunState state, AgentRun run, CancellationToken cancellationToken)
    {
        state.Run = run;
        await services.Runs.UpdateRunAsync(run, cancellationToken);
    }

    private static string Cap(string text) => text.Length > AgentContractLimits.ErrorMaxLength ? text[..AgentContractLimits.ErrorMaxLength] : text;
}

/// <summary>A UI tool call (tool and canonical arguments).</summary>
/// <param name="Tool">UI tool name.</param>
/// <param name="Arguments">Canonical JSON arguments.</param>
internal sealed record UiCall(string Tool, string Arguments);

/// <summary>Page changes caused by navigation tools, so the next turn offers that page's tools (tool catalogue "Selection").</summary>
internal static class PageTransitions
{
    /// <summary>Updates the run's page after a successful navigation tool.</summary>
    public static void Apply(RunState state, RunStep step)
    {
        using var document = JsonDocument.Parse(step.Arguments);
        var arguments = document.RootElement;
        state.Page = step.Tool switch
        {
            "open_mixer" => "mixer",
            "open_track" => "track",
            "navigate" when arguments.TryGetProperty("page", out var page) && page.ValueKind == JsonValueKind.String => page.GetString()!,
            _ => state.Page,
        };
    }
}

/// <summary>The open page reported that it could not execute a UI tool; its text may contain external titles.</summary>
internal sealed class UiToolFailedException : Exception
{
    public UiToolFailedException()
        : this(StepFailure.PageFailed)
    {
    }

    public UiToolFailedException(string message)
        : base(message)
    {
    }

    public UiToolFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A failed step: the readable step error for the panel and what the model sees. Raw exception texts are only logged; texts
/// that may carry external content (page errors) reach the model only as untrusted_text, which taints the run.
/// </summary>
/// <param name="StepError">Error stored on the step.</param>
/// <param name="Untrusted">External text for the model, when any.</param>
internal sealed record StepFailure(string StepError, string? Untrusted)
{
    /// <summary>Reason for an unexpected error.</summary>
    public const string Internal = "The step failed because of an internal error.";

    /// <summary>Trusted message for a page that could not execute a UI tool.</summary>
    public const string PageFailed = "The page could not execute the step.";

    private const int UntrustedMaxLength = 300;

    /// <summary>The failure of an exception of a handler or a UI result.</summary>
    public static StepFailure From(Exception exception, Microsoft.Extensions.Logging.ILogger logger, Guid runId, string tool) => exception switch
    {
        UiToolFailedException page => new StepFailure(page.Message, page.Message),
        AgentActionException action => new StepFailure(action.Message, null),
        JsonException => new StepFailure("The page returned an invalid result.", null),
        HttpRequestException => new StepFailure("StemStudio could not be reached.", null),
        TimeoutException or OperationCanceledException => new StepFailure("The step timed out.", null),
        _ => Logged(exception, logger, runId, tool),
    };

    /// <summary>What the model sees.</summary>
    public ProjectedResult ModelResult() => ResultProjector.Untrusted(Untrusted, UntrustedMaxLength) is { } external
        ? ResultProjector.Project(new JsonObject { ["ok"] = false, ["error"] = PageFailed, [ResultProjector.UntrustedField] = external })
        : ResultProjector.Error(StepError);

    private static StepFailure Logged(Exception exception, Microsoft.Extensions.Logging.ILogger logger, Guid runId, string tool)
    {
        RunLog.StepFailed(logger, runId, tool, exception);
        return new StepFailure(Internal, null);
    }
}
