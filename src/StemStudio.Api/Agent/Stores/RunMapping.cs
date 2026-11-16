using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;
using StemStudio.Persistence.Agent;

namespace StemStudio.Api.Agent.Stores;

/// <summary>Maps runs and approvals between the agent records and the persistence rows.</summary>
internal static class RunMapping
{
    /// <summary>Copies a run onto a row.</summary>
    /// <param name="entity">Target row.</param>
    /// <param name="run">Source record.</param>
    /// <returns>The row.</returns>
    public static AgentRunEntity Apply(AgentRunEntity entity, AgentRun run)
    {
        entity.Id = run.Id;
        entity.ConversationId = run.ConversationId;
        entity.UserMessageId = run.UserMessageId;
        entity.Backend = run.Backend.ToString();
        entity.Model = run.Model;
        ApplyState(entity, run);
        ApplyCounters(entity, run);
        return entity;
    }

    /// <summary>Reads a run row.</summary>
    /// <param name="entity">The row.</param>
    /// <returns>The record.</returns>
    public static AgentRun ToRecord(AgentRunEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        UserMessageId = entity.UserMessageId,
        Status = AgentStoreValues.Parse<AgentRunStatusDto>(entity.Status),
        Backend = AgentStoreValues.Parse<AgentBackendDto>(entity.Backend),
        Model = entity.Model,
        Tainted = entity.Tainted,
        Plan = AgentStoreValues.FromJson<List<PlanItem>>(entity.Plan),
        ModelTurns = entity.ModelTurns,
        ToolCalls = entity.ToolCalls,
        InvalidCalls = entity.InvalidCalls,
        Usage = new TokenUsage(entity.InputTokens, entity.OutputTokens, entity.CachedInputTokens),
        FailureCode = entity.FailureCode,
        FailureMessage = entity.FailureMessage,
        StartedAt = AgentStoreValues.Offset(entity.StartedAt),
        EndedAt = AgentStoreValues.Offset(entity.EndedAt),
    };

    /// <summary>Copies an approval onto a row.</summary>
    /// <param name="entity">Target row.</param>
    /// <param name="approval">Source record.</param>
    /// <returns>The row.</returns>
    public static ApprovalEntity Apply(ApprovalEntity entity, Approval approval)
    {
        entity.Id = approval.Id;
        entity.RunId = approval.RunId;
        entity.Status = approval.Status.ToString();
        entity.Scope = approval.Scope.ToString();
        entity.ArgumentsHash = approval.ArgumentsHash;
        entity.RequestedAt = AgentStoreValues.Utc(approval.RequestedAt);
        entity.DecidedAt = AgentStoreValues.Utc(approval.DecidedAt);
        return entity;
    }

    /// <summary>Reads an approval row.</summary>
    /// <param name="entity">The row.</param>
    /// <returns>The record.</returns>
    public static Approval ToRecord(ApprovalEntity entity) => new()
    {
        Id = entity.Id,
        RunId = entity.RunId,
        Status = AgentStoreValues.Parse<ApprovalStatusDto>(entity.Status),
        Scope = AgentStoreValues.Parse<ApprovalScopeDto>(entity.Scope),
        ArgumentsHash = entity.ArgumentsHash,
        RequestedAt = AgentStoreValues.Offset(entity.RequestedAt),
        DecidedAt = AgentStoreValues.Offset(entity.DecidedAt),
    };

    private static void ApplyState(AgentRunEntity entity, AgentRun run)
    {
        entity.Status = run.Status.ToString();
        entity.Tainted = run.Tainted;
        entity.Plan = AgentStoreValues.ToJson(run.Plan);
        entity.FailureCode = run.FailureCode;
        entity.FailureMessage = run.FailureMessage;
        entity.StartedAt = AgentStoreValues.Utc(run.StartedAt);
        entity.EndedAt = AgentStoreValues.Utc(run.EndedAt);
    }

    private static void ApplyCounters(AgentRunEntity entity, AgentRun run)
    {
        entity.ModelTurns = run.ModelTurns;
        entity.ToolCalls = run.ToolCalls;
        entity.InvalidCalls = run.InvalidCalls;
        entity.InputTokens = run.Usage.InputTokens;
        entity.OutputTokens = run.Usage.OutputTokens;
        entity.CachedInputTokens = run.Usage.CachedInputTokens;
    }
}
