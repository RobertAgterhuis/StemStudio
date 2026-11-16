using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;
using StemStudio.Persistence.Agent;

namespace StemStudio.Api.Agent.Stores;

/// <summary>Maps run steps and undo entries between the agent records and the persistence rows.</summary>
internal static class StepMapping
{
    /// <summary>Copies a step onto a row.</summary>
    /// <param name="entity">Target row.</param>
    /// <param name="step">Source record.</param>
    /// <returns>The row.</returns>
    public static RunStepEntity Apply(RunStepEntity entity, RunStep step)
    {
        entity.Id = step.Id;
        entity.RunId = step.RunId;
        entity.Seq = step.Seq;
        entity.CallId = step.CallId;
        ApplyCall(entity, step);
        ApplyProgress(entity, step);
        return entity;
    }

    private static void ApplyCall(RunStepEntity entity, RunStep step)
    {
        entity.Tool = step.Tool;
        entity.Kind = step.Kind.ToString();
        entity.Risk = step.Risk.ToString();
        entity.UndoKind = step.UndoKind.ToString();
        entity.Arguments = step.Arguments;
        entity.ArgumentsHash = step.ArgumentsHash;
        entity.PlanIndex = step.PlanIndex;
        entity.Decision = step.Decision.ToString();
        entity.DecisionReasons = AgentStoreValues.ToJson(step.DecisionReasons);
    }

    /// <summary>Reads a step row.</summary>
    /// <param name="entity">The row.</param>
    /// <returns>The record.</returns>
    public static RunStep ToRecord(RunStepEntity entity) => new()
    {
        Id = entity.Id, RunId = entity.RunId, Seq = entity.Seq, CallId = entity.CallId, Tool = entity.Tool,
        Kind = AgentStoreValues.Parse<ToolKindDto>(entity.Kind), Risk = AgentStoreValues.Parse<ToolRiskDto>(entity.Risk), UndoKind = AgentStoreValues.Parse<UndoKindDto>(entity.UndoKind),
        Arguments = entity.Arguments, ArgumentsHash = entity.ArgumentsHash, PlanIndex = entity.PlanIndex,
        Decision = AgentStoreValues.Parse<StepDecisionDto>(entity.Decision), DecisionReasons = AgentStoreValues.FromJson<List<string>>(entity.DecisionReasons),
        Status = AgentStoreValues.Parse<AgentStepStatusDto>(entity.Status), StartedAt = AgentStoreValues.Offset(entity.StartedAt), EndedAt = AgentStoreValues.Offset(entity.EndedAt),
        EstimatedMinutes = entity.EstimatedMinutes, ResultSummary = entity.ResultSummary, Error = entity.Error, Progress = entity.Progress,
        Job = JobOf(entity),
    };

    /// <summary>Creates an undo row.</summary>
    /// <param name="entry">The record.</param>
    /// <param name="ordinal">Insertion order within the conversation.</param>
    /// <returns>The row.</returns>
    public static UndoEntryEntity ToEntity(UndoEntry entry, int ordinal) => new()
    {
        Id = entry.Id,
        ConversationId = entry.ConversationId,
        RunId = entry.RunId,
        StepId = entry.StepId,
        Ordinal = ordinal,
        Kind = entry.Kind.ToString(),
        ActionTool = entry.Action.Tool,
        ActionArguments = entry.Action.Arguments,
        Fresh = entry.Fresh,
        Status = entry.Status.ToString(),
    };

    /// <summary>Reads an undo row.</summary>
    /// <param name="entity">The row.</param>
    /// <returns>The record.</returns>
    public static UndoEntry ToRecord(UndoEntryEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        RunId = entity.RunId,
        StepId = entity.StepId,
        Kind = AgentStoreValues.Parse<UndoKindDto>(entity.Kind),
        Action = new UndoAction { Tool = entity.ActionTool, Arguments = entity.ActionArguments },
        Fresh = entity.Fresh,
        Status = AgentStoreValues.Parse<UndoEntryStatus>(entity.Status),
    };

    private static void ApplyProgress(RunStepEntity entity, RunStep step)
    {
        entity.Status = step.Status.ToString();
        entity.StartedAt = AgentStoreValues.Utc(step.StartedAt);
        entity.EndedAt = AgentStoreValues.Utc(step.EndedAt);
        entity.EstimatedMinutes = step.EstimatedMinutes;
        entity.ResultSummary = step.ResultSummary;
        entity.Error = step.Error;
        entity.Progress = step.Progress;
        entity.JobTrackId = step.Job?.TrackId;
        entity.JobId = step.Job?.JobId;
        entity.JobPhase = step.Job?.Phase.ToString();
    }

    private static JobRef? JobOf(RunStepEntity entity) =>
        AgentStoreValues.ParseOptional<JobPhaseDto>(entity.JobPhase) is { } phase ? new JobRef { TrackId = entity.JobTrackId, JobId = entity.JobId, Phase = phase } : null;
}
