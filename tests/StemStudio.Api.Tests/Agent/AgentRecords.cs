using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Tests.Agent;

/// <summary>Builders of agent records (the StemStudio.Agent model) with every field set.</summary>
internal static class AgentRecords
{
    public static readonly DateTimeOffset T0 = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    public static Conversation Conversation(DateTimeOffset? updatedAt = null, string title = "Mute the drums") => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        CreatedAt = T0,
        UpdatedAt = updatedAt ?? T0,
    };

    public static ConversationMessage Message(Guid conversationId, int seq, MessageRoleDto role = MessageRoleDto.User, Guid? runId = null) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Seq = seq,
        Role = role,
        Text = $"Message {seq}",
        Context = new PageContextDto { Page = "mixer", TrackId = Guid.NewGuid(), MixId = null },
        RunId = runId,
        CreatedAt = T0.AddSeconds(seq),
    };

    public static AgentRun Run(Guid conversationId, AgentRunStatusDto status = AgentRunStatusDto.Queued) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        UserMessageId = Guid.NewGuid(),
        Status = status,
        Backend = AgentBackendDto.Ollama,
        Model = "qwen3.5:9b",
        Tainted = false,
        Plan = new List<PlanItem> { new() { Index = 0, Title = "Mute drums", Tool = "mixer_set_channel", Status = PlanItemStatusDto.Pending } },
        ModelTurns = 1,
        ToolCalls = 2,
        InvalidCalls = 0,
        Usage = new TokenUsage(100, 20, 50),
        StartedAt = T0,
    };

    public static RunStep Step(Guid runId, int seq, AgentStepStatusDto status = AgentStepStatusDto.Running) => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        Seq = seq,
        CallId = $"call-1-{seq}",
        Tool = "delete_mix",
        Kind = ToolKindDto.Server,
        Risk = ToolRiskDto.Destructive,
        UndoKind = UndoKindDto.Compensate,
        Arguments = """{"mixId":"5c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f"}""",
        ArgumentsHash = new string('a', 64),
        PlanIndex = 0,
        Decision = StepDecisionDto.ApprovalRequired,
        DecisionReasons = new List<string> { AgentDecisionReasons.RiskDestructive },
        Status = status,
        StartedAt = T0,
        EstimatedMinutes = 2.5,
        ResultSummary = """{"ok":true}""",
        Progress = 10,
        Job = new JobRef { TrackId = Guid.NewGuid(), JobId = "job-1", Phase = JobPhaseDto.Detail },
    };

    public static Approval Approval(Guid stepId, Guid runId, ApprovalStatusDto status = ApprovalStatusDto.Pending) => new()
    {
        Id = stepId,
        RunId = runId,
        Status = status,
        Scope = ApprovalScopeDto.Once,
        ArgumentsHash = new string('a', 64),
        RequestedAt = T0,
    };

    public static UndoEntry Undo(Guid conversationId, Guid runId, string tool) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        RunId = runId,
        StepId = Guid.NewGuid(),
        Kind = UndoKindDto.Inverse,
        Action = new UndoAction { Tool = tool, Arguments = """{"stem":"drums"}""" },
        Fresh = false,
        Status = UndoEntryStatus.Available,
    };
}
