using StemStudio.Persistence.Agent;

namespace StemStudio.Persistence.Tests.Agent;

/// <summary>Builders of agent rows with every column set.</summary>
internal static class AgentRows
{
    public static readonly Guid TrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");
    public static readonly Guid MixId = Guid.Parse("5c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f");

    public static AgentSettingsEntity Settings() => new()
    {
        Backend = "Ollama",
        OllamaModel = "qwen3.5:9b",
        OllamaFallbackModel = "qwen3.5:4b",
        OllamaThink = true,
        ClaudeModel = "ClaudeSonnet55",
        ClaudeEffort = "Medium",
        ClaudeNoticeAcknowledgedAt = TestData.T0,
        Limits = """{"maxModelTurns":10}""",
        UpdatedAt = TestData.T0.AddMinutes(1),
    };

    public static ConversationEntity Conversation(DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Mute the drums",
        CreatedAt = now,
        UpdatedAt = now.AddMinutes(1),
    };

    public static ConversationMessageEntity Message(Guid conversationId, int seq, string role, Guid? runId) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Seq = seq,
        Role = role,
        Text = "Mute the drums",
        Page = "mixer",
        TrackId = TrackId,
        MixId = MixId,
        RunId = runId,
        CreatedAt = TestData.T0,
    };

    public static AgentRunEntity Run(Guid conversationId, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        UserMessageId = Guid.NewGuid(),
        Status = "AwaitingApproval",
        Backend = "Ollama",
        Model = "qwen3.5:9b",
        Tainted = true,
        Plan = """[{"index":0,"title":"Mute drums","tool":"mixer_set_channel","status":"Running"}]""",
        ModelTurns = 2,
        ToolCalls = 3,
        InvalidCalls = 1,
        InputTokens = 1200,
        OutputTokens = 80,
        CachedInputTokens = 900,
        FailureCode = "timeout",
        FailureMessage = "The model did not answer.",
        StartedAt = now,
        EndedAt = now.AddMinutes(2),
    };

    public static RunStepEntity Step(Guid runId, int seq) => new()
    {
        Id = Guid.NewGuid(),
        RunId = runId,
        Seq = seq,
        CallId = "call-1-1",
        Tool = "delete_mix",
        Kind = "Server",
        Risk = "Destructive",
        UndoKind = "Compensate",
        Arguments = """{"mixId":"5c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f"}""",
        ArgumentsHash = new string('a', 64),
        PlanIndex = 0,
        Decision = "ApprovalRequired",
        DecisionReasons = """["risk-destructive","tainted"]""",
        Status = "AwaitingApproval",
        StartedAt = TestData.T0,
        EndedAt = TestData.T0.AddSeconds(5),
        EstimatedMinutes = 4.5,
        ResultSummary = """{"ok":true}""",
        Error = "None",
        Progress = 40,
        JobTrackId = TrackId,
        JobId = "3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c",
        JobPhase = "Detail",
    };

    public static ApprovalEntity Approval(Guid stepId, Guid runId, DateTime now) => new()
    {
        Id = stepId,
        RunId = runId,
        Status = "Approved",
        Scope = "PlanHeavy",
        ArgumentsHash = new string('a', 64),
        RequestedAt = now,
        DecidedAt = now.AddSeconds(30),
    };

    public static UndoEntryEntity Undo(Guid conversationId, Guid runId, Guid stepId, int ordinal) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        RunId = runId,
        StepId = stepId,
        Ordinal = ordinal,
        Kind = "Inverse",
        ActionTool = "mixer_set_channel",
        ActionArguments = """{"stem":"drums","mute":false}""",
        Fresh = true,
        Status = "Available",
    };
}
