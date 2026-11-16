using System.Text.Json;
using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>Maps the agent records to the response DTOs of backend 1.10.0.</summary>
public static class AgentDtoMapping
{
    /// <summary>Conversation summary.</summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="lastRunStatus">Status of its newest run, if any.</param>
    /// <returns>The DTO.</returns>
    public static ConversationSummaryDto ToSummary(Conversation conversation, AgentRunStatusDto? lastRunStatus)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        return new ConversationSummaryDto
        {
            ConversationId = conversation.Id,
            Title = conversation.Title,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
            LastRunStatus = lastRunStatus,
        };
    }

    /// <summary>Message DTO.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The DTO.</returns>
    public static AgentMessageDto ToDto(ConversationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new AgentMessageDto
        {
            MessageId = message.Id,
            Seq = message.Seq,
            Role = message.Role,
            Text = message.Text,
            Context = message.Context,
            RunId = message.RunId,
            CreatedAt = message.CreatedAt,
        };
    }

    /// <summary>Run summary of a conversation.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The DTO.</returns>
    public static AgentRunSummaryDto ToSummary(AgentRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new AgentRunSummaryDto { RunId = run.Id, MessageId = run.UserMessageId, Status = run.Status, StartedAt = run.StartedAt, EndedAt = run.EndedAt };
    }

    /// <summary>Run snapshot.</summary>
    /// <param name="run">The run.</param>
    /// <param name="steps">Its steps in Seq order.</param>
    /// <param name="pendingApproval">The open approval card, if any.</param>
    /// <returns>The DTO.</returns>
    public static AgentRunDto ToDto(AgentRun run, IReadOnlyList<RunStep> steps, ApprovalRequestDto? pendingApproval)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new AgentRunDto
        {
            RunId = run.Id, ConversationId = run.ConversationId, Status = run.Status, Backend = run.Backend, Model = run.Model, Tainted = run.Tainted,
            Plan = [.. run.Plan.Select(item => new PlanItemDto { Index = item.Index, Title = item.Title, Tool = item.Tool, Status = item.Status })],
            Steps = [.. steps.Select(ToDto)],
            PendingApproval = pendingApproval,
            Usage = new TokenUsageDto { InputTokens = run.Usage.InputTokens, OutputTokens = run.Usage.OutputTokens, CachedInputTokens = run.Usage.CachedInputTokens },
            Failure = run.FailureCode is null ? null : new RunFailureDto { Code = run.FailureCode, Message = run.FailureMessage ?? string.Empty },
            StartedAt = run.StartedAt, EndedAt = run.EndedAt,
        };
    }

    /// <summary>Step DTO.</summary>
    /// <param name="step">The step.</param>
    /// <returns>The DTO.</returns>
    public static AgentStepDto ToDto(RunStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new AgentStepDto
        {
            StepId = step.Id, Seq = step.Seq, Tool = step.Tool, Kind = step.Kind, Risk = step.Risk, Undo = step.UndoKind,
            Arguments = Json(step.Arguments), ArgumentsHash = step.ArgumentsHash, PlanIndex = step.PlanIndex,
            Decision = step.Decision, DecisionReasons = step.DecisionReasons, Status = step.Status,
            ResultSummary = step.ResultSummary is null ? null : Json(step.ResultSummary), Error = step.Error,
            Job = step.Job is null ? null : new JobRefDto { TrackId = step.Job.TrackId, JobId = step.Job.JobId, Phase = step.Job.Phase },
            Progress = step.Progress, EstimatedMinutes = step.EstimatedMinutes, StartedAt = step.StartedAt, EndedAt = step.EndedAt,
        };
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
