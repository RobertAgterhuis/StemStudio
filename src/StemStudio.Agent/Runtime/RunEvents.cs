using System.Text.Json;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>Publishes the DELTA.md section 3 events as camelCase JSON and maps records to their contract views.</summary>
/// <param name="sink">The event sink.</param>
internal sealed class RunEvents(IRunEventSink sink)
{
    /// <summary>Contract JSON (camelCase, enum names from the DTO attributes).</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) { Encoder = CanonicalJson.Encoder };

    /// <summary>Publishes one event.</summary>
    public Task PublishAsync<T>(Guid runId, string type, T payload, CancellationToken cancellationToken) =>
        sink.PublishAsync(runId, type, JsonSerializer.Serialize(payload, Json), cancellationToken);

    /// <summary>step.created with the full step.</summary>
    public Task StepCreatedAsync(RunStep step, CancellationToken cancellationToken) =>
        PublishAsync(step.RunId, AgentEventTypes.StepCreated, ToDto(step), cancellationToken);

    /// <summary>step.updated with status, progress, result and error.</summary>
    public Task StepUpdatedAsync(RunStep step, CancellationToken cancellationToken) =>
        PublishAsync(step.RunId, AgentEventTypes.StepUpdated, new StepUpdatedEventDto
        {
            StepId = step.Id,
            Status = step.Status,
            Progress = step.Progress,
            ResultSummary = Element(step.ResultSummary),
            Error = step.Error,
        }, cancellationToken);

    /// <summary>The contract view of a step.</summary>
    public static AgentStepDto ToDto(RunStep step) => new()
    {
        StepId = step.Id,
        Seq = step.Seq,
        Tool = step.Tool,
        Kind = step.Kind,
        Risk = step.Risk,
        Undo = step.UndoKind,
        Arguments = Element(step.Arguments) ?? default,
        ArgumentsHash = step.ArgumentsHash,
        PlanIndex = step.PlanIndex,
        Decision = step.Decision,
        DecisionReasons = step.DecisionReasons,
        Status = step.Status,
        ResultSummary = Element(step.ResultSummary),
        Error = step.Error,
        Job = step.Job is { } job ? new JobRefDto { TrackId = job.TrackId, JobId = job.JobId, Phase = job.Phase } : null,
        Progress = step.Progress,
        EstimatedMinutes = step.EstimatedMinutes,
        StartedAt = step.StartedAt,
        EndedAt = step.EndedAt,
    };

    /// <summary>The contract view of a plan.</summary>
    public static PlanUpdatedEventDto ToDto(IReadOnlyList<PlanItem> plan) => new()
    {
        Plan = [.. plan.Select(item => new PlanItemDto { Index = item.Index, Title = item.Title, Tool = item.Tool, Status = item.Status })],
    };

    /// <summary>Parses stored JSON for an event payload.</summary>
    public static JsonElement? Element(string? json) => json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
}
