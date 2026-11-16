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
internal sealed class StepExecutor(AgentServices services, RunEvents events)
{
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(AgentContractLimits.UiToolTimeoutSeconds);

    /// <summary>Executes the call and returns what the model sees.</summary>
    public async Task<FunctionResultContent> ExecuteAsync(RunState state, FunctionCallContent call, IReadOnlyList<AgentToolDescriptor> offered, CancellationToken cancellationToken)
    {
        await CountCallAsync(state, cancellationToken);
        var prepared = PreparedCall.From(call, offered);
        var result = prepared.IsValid ? await DecideAndRunAsync(state, prepared, cancellationToken) : await RejectInvalidAsync(state, prepared, cancellationToken);
        return new FunctionResultContent(call.CallId, result.Json);
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

    private async Task<ProjectedResult> DecideAndRunAsync(RunState state, PreparedCall prepared, CancellationToken cancellationToken)
    {
        var decision = ToolPolicy.Decide(PolicyFor(state, prepared));
        var status = decision.Decision switch
        {
            StepDecisionDto.Denied => AgentStepStatusDto.Failed,
            StepDecisionDto.ApprovalRequired => AgentStepStatusDto.AwaitingApproval,
            _ => AgentStepStatusDto.Running,
        };
        var step = prepared.ToStep(state.RunId, ++state.StepSeq, status) with { Decision = decision.Decision, DecisionReasons = decision.Reasons, Error = decision.Refusal };
        step = Started(step, status);
        await services.Runs.SaveStepAsync(step, cancellationToken);
        await events.StepCreatedAsync(step, cancellationToken);
        return decision.Decision switch
        {
            StepDecisionDto.Denied => ResultProjector.Error(decision.Refusal!),
            StepDecisionDto.ApprovalRequired => await FinishAsync(step with { Status = AgentStepStatusDto.Rejected, Error = "This step needs the user's approval." }, null, cancellationToken),
            _ => await RunAsync(state, step, prepared, cancellationToken),
        };
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
    };

    private async Task<ProjectedResult> RunAsync(RunState state, RunStep step, PreparedCall prepared, CancellationToken cancellationToken)
    {
        try
        {
            var result = await InvokeAsync(state, step, prepared, cancellationToken);
            return await SucceedAsync(state, step, prepared, result, cancellationToken);
        }
        catch (AgentActionException exception)
        {
            var failed = step with { Status = AgentStepStatusDto.Failed, Error = Cap(exception.Message) };
            await FinishAsync(failed, null, cancellationToken);
            return ResultProjector.Error(exception.Message);
        }
    }

    private Task<ToolResult> InvokeAsync(RunState state, RunStep step, PreparedCall prepared, CancellationToken cancellationToken) => prepared.Tool!.Kind switch
    {
        ToolKindDto.Ui => RunUiToolAsync(state, step, prepared, cancellationToken),
        ToolKindDto.Meta => throw new AgentActionException("This tool is not available yet."),
        _ => prepared.Tool.Handler!(Invocation(state, step, prepared), cancellationToken),
    };

    private ToolInvocation Invocation(RunState state, RunStep step, PreparedCall prepared) => new()
    {
        Arguments = prepared.Validation.Arguments,
        Actions = services.Actions,
        Context = state.Context,
        Backend = state.Run.Backend,
        Ui = (tool, arguments, cancellationToken) => RequestUiAsync(state, step, tool, arguments, cancellationToken),
    };

    private async Task<ToolResult> RunUiToolAsync(RunState state, RunStep step, PreparedCall prepared, CancellationToken cancellationToken)
    {
        var answer = await RequestUiAsync(state, step, step.Tool, step.Arguments, cancellationToken);
        if (!answer.Ok)
        {
            throw new AgentActionException(answer.Error ?? "The page could not execute the step.");
        }

        var element = RunEvents.Element(answer.Result);
        return prepared.Tool!.MapUiResult?.Invoke(prepared.Validation.Arguments, element)
            ?? ToolResult.Of(JsonNode.Parse(answer.Result ?? "{}") ?? new JsonObject());
    }

    private async Task<UiToolResult> RequestUiAsync(RunState state, RunStep step, string tool, string arguments, CancellationToken cancellationToken)
    {
        var waiting = step with { Status = AgentStepStatusDto.AwaitingUi };
        await services.Runs.SaveStepAsync(waiting, cancellationToken);
        await events.StepUpdatedAsync(waiting, cancellationToken);
        await SaveRunAsync(state, state.Run with { Status = AgentRunStatusDto.AwaitingUi }, cancellationToken);
        var payload = new UiRequestedEventDto { StepId = step.Id, Tool = tool, Arguments = RunEvents.Element(arguments)!.Value, TimeoutSeconds = AgentContractLimits.UiToolTimeoutSeconds };
        await events.PublishAsync(state.RunId, AgentEventTypes.UiRequested, payload, cancellationToken);
        var request = new UiToolRequest { RunId = state.RunId, StepId = step.Id, Tool = tool, Arguments = arguments, Timeout = UiTimeout };
        var answer = await services.Ui.RequestAsync(request, cancellationToken);
        await SaveRunAsync(state, state.Run with { Status = AgentRunStatusDto.Running }, cancellationToken);
        return answer;
    }

    private async Task<ProjectedResult> SucceedAsync(RunState state, RunStep step, PreparedCall prepared, ToolResult result, CancellationToken cancellationToken)
    {
        var projected = ResultProjector.Project(result.Projection);
        if (projected.Tainted && !state.Run.Tainted)
        {
            await SaveRunAsync(state, state.Run with { Tainted = true }, cancellationToken);
        }

        PageTransitions.Apply(state, step.Tool, prepared.Validation.Arguments);
        var done = step with { Status = AgentStepStatusDto.Succeeded, Job = result.Job ?? step.Job, EstimatedMinutes = result.EstimatedMinutes ?? step.EstimatedMinutes };
        await FinishAsync(done, projected.Json, cancellationToken);
        return projected;
    }

    private async Task<ProjectedResult> FinishAsync(RunStep step, string? resultJson, CancellationToken cancellationToken)
    {
        var finished = step with { ResultSummary = resultJson, EndedAt = services.Time.GetUtcNow() };
        await services.Runs.SaveStepAsync(finished, cancellationToken);
        await events.StepUpdatedAsync(finished, cancellationToken);
        return resultJson is null ? ResultProjector.Error(step.Error ?? "The step did not run.") : new ProjectedResult(resultJson, false, false);
    }

    private async Task SaveRunAsync(RunState state, AgentRun run, CancellationToken cancellationToken)
    {
        state.Run = run;
        await services.Runs.UpdateRunAsync(run, cancellationToken);
    }

    private static string Cap(string text) => text.Length > AgentContractLimits.ErrorMaxLength ? text[..AgentContractLimits.ErrorMaxLength] : text;
}

/// <summary>Page changes caused by navigation tools, so the next turn offers that page's tools (tool catalogue "Selection").</summary>
internal static class PageTransitions
{
    /// <summary>Updates the run's page after a successful navigation tool.</summary>
    public static void Apply(RunState state, string tool, JsonElement arguments)
    {
        state.Page = tool switch
        {
            "open_mixer" => "mixer",
            "open_track" => "track",
            "navigate" when arguments.TryGetProperty("page", out var page) && page.ValueKind == JsonValueKind.String => page.GetString()!,
            _ => state.Page,
        };
    }
}
