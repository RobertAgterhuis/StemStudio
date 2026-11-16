using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>Keeps the run's plan persisted and published (<c>plan.updated</c>) as calls are linked and finish.</summary>
/// <param name="services">Ports.</param>
/// <param name="events">Event publisher.</param>
internal sealed class RunPlan(AgentServices services, RunEvents events)
{
    /// <summary>Links a non-meta call to the plan; null without a plan.</summary>
    public async Task<int?> LinkAsync(RunState state, AgentToolDescriptor tool, CancellationToken cancellationToken)
    {
        if (tool.Kind == ToolKindDto.Meta || state.Run.Plan.Count == 0)
        {
            return null;
        }

        var link = PlanTracker.Link(state.Run.Plan, tool.Name, tool.Title);
        await PublishAsync(state, link.Plan, cancellationToken);
        return link.Index;
    }

    /// <summary>Sets the status of a linked item.</summary>
    public Task SetAsync(RunState state, int? index, PlanItemStatusDto status, CancellationToken cancellationToken) =>
        index is null ? Task.CompletedTask : PublishAsync(state, PlanTracker.SetStatus(state.Run.Plan, index, status), cancellationToken);

    /// <summary>Executes set_plan.</summary>
    public async Task<ToolResult> SetPlanAsync(RunState state, JsonElement arguments, CancellationToken cancellationToken)
    {
        var steps = arguments.GetProperty("steps").EnumerateArray()
            .Select(step => new PlanStep(step.GetProperty("title").GetString()!, step.TryGetProperty("tool", out var tool) ? tool.GetString() : null))
            .ToList();
        await PublishAsync(state, PlanTracker.Replace(state.Run.Plan, steps), cancellationToken);
        return ToolResult.Of(new JsonObject { ["accepted"] = steps.Count });
    }

    private async Task PublishAsync(RunState state, IReadOnlyList<PlanItem> plan, CancellationToken cancellationToken)
    {
        state.Run = state.Run with { Plan = plan };
        await services.Runs.UpdateRunAsync(state.Run, cancellationToken);
        await events.PublishAsync(state.RunId, AgentEventTypes.PlanUpdated, RunEvents.ToDto(plan), cancellationToken);
    }
}

/// <summary>Executes the meta tools (<c>set_plan</c>, <c>wait_for_job</c>).</summary>
/// <param name="services">Ports.</param>
/// <param name="events">Event publisher.</param>
/// <param name="plan">Plan keeper.</param>
internal sealed class MetaToolRunner(AgentServices services, RunEvents events, RunPlan plan)
{
    private const int UntrustedMaxLength = 300;
    private readonly JobWaiter _jobs = new(services.Actions, services.Time);

    /// <summary>Executes a meta tool.</summary>
    public Task<ToolResult> ExecuteAsync(RunState state, RunStep step, JsonElement arguments, CancellationToken cancellationToken) => step.Tool switch
    {
        MetaToolProvider.SetPlan => plan.SetPlanAsync(state, arguments, cancellationToken),
        _ => WaitAsync(state, step, arguments, cancellationToken),
    };

    private async Task<ToolResult> WaitAsync(RunState state, RunStep step, JsonElement arguments, CancellationToken cancellationToken)
    {
        var job = JobOf(arguments);
        var wait = new JobWait(job, TimeSpan.FromMinutes(state.Limits.MaxJobWaitMinutes)) { OnProgress = (poll, token) => ProgressAsync(state, step with { Job = job }, poll, token) };
        var result = await _jobs.WaitAsync(wait, cancellationToken);
        await SetRunStatusAsync(state, AgentRunStatusDto.Running, cancellationToken);
        return new ToolResult { Projection = Projection(result), Job = job, Progress = result.Progress };
    }

    private static JobRef JobOf(JsonElement arguments) => new()
    {
        Phase = JsonSerializer.Deserialize<JobPhaseDto>(JsonSerializer.Serialize(arguments.GetProperty("phase").GetString())),
        TrackId = arguments.TryGetProperty("trackId", out var track) ? Guid.Parse(track.GetString()!) : null,
        JobId = arguments.TryGetProperty("jobId", out var id) ? id.GetString() : null,
    };

    /// <summary>Status and progress (trusted), the readable error and any external text marked untrusted.</summary>
    private static JsonObject Projection(JobPoll result)
    {
        var projection = new JsonObject { ["status"] = result.Status, ["progress"] = result.Progress };
        if (result.Error is { } error)
        {
            projection["error"] = error;
        }

        if (ResultProjector.Untrusted(result.Untrusted, UntrustedMaxLength) is { } external)
        {
            projection[ResultProjector.UntrustedField] = external;
        }

        return projection;
    }

    private async Task ProgressAsync(RunState state, RunStep step, JobPoll poll, CancellationToken cancellationToken)
    {
        var waiting = step with { Status = AgentStepStatusDto.WaitingJob, Progress = poll.Progress };
        await services.Runs.SaveStepAsync(waiting, cancellationToken);
        await events.StepUpdatedAsync(waiting, cancellationToken);
        await SetRunStatusAsync(state, AgentRunStatusDto.WaitingJob, cancellationToken);
    }

    private async Task SetRunStatusAsync(RunState state, AgentRunStatusDto status, CancellationToken cancellationToken)
    {
        if (state.Run.Status != status)
        {
            state.Run = state.Run with { Status = status };
            await services.Runs.UpdateRunAsync(state.Run, cancellationToken);
        }
    }
}
