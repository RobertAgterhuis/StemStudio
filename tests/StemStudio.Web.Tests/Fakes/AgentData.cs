using System.Text.Json;
using StemStudio.Contracts.Agent;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Contract-shaped agent DTOs (backend-api 1.10.0, feature 011).</summary>
public static class AgentData
{
    public static readonly Guid ConversationId = Guid.Parse("a1b2c3d4-0000-4000-8000-000000000001");
    public static readonly Guid OtherConversationId = Guid.Parse("a1b2c3d4-0000-4000-8000-000000000002");
    public static readonly Guid RunId = Guid.Parse("b1b2c3d4-0000-4000-8000-000000000001");
    public static readonly Guid MessageId = Guid.Parse("c1b2c3d4-0000-4000-8000-000000000001");
    public static readonly Guid StepId = Guid.Parse("d1b2c3d4-0000-4000-8000-000000000001");
    public static readonly Guid TrackId = Guid.Parse("e1b2c3d4-0000-4000-8000-000000000001");
    public static readonly Guid MixId = Guid.Parse("f1b2c3d4-0000-4000-8000-000000000001");
    public static readonly DateTimeOffset At = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    public static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    public static string Serialize(object value) => JsonSerializer.Serialize(value, JsonSerializerOptions.Web);

    public static AgentStatusDto Status(bool ready = true, AgentBackendDto backend = AgentBackendDto.Ollama) => new()
    {
        Backend = backend,
        Ready = ready,
        ActiveModel = ready ? "qwen3.5:9b" : null,
        UsingFallback = false,
        Reason = ready ? null : "Ollama not reachable at http://host.docker.internal:11434",
        Fix = ready ? null : "Start Ollama",
        Ollama = Ollama(),
        Claude = new ClaudeStatusDto { KeyConfigured = false, NoticeAcknowledged = false, Model = ClaudeModelDto.ClaudeOpus55 },
    };

    public static OllamaStatusDto Ollama(bool reachable = true) => new()
    {
        Reachable = reachable,
        Version = reachable ? "0.40.16" : null,
        Models = reachable
            ?
            [
                new OllamaModelInfoDto { Name = "qwen3.5:9b", SizeBytes = 6_594_474_711, Tools = true },
                new OllamaModelInfoDto { Name = "qwen3.5:4b", SizeBytes = 3_324_173_934, Tools = true },
                new OllamaModelInfoDto { Name = "llava:7b", SizeBytes = 4_000_000_000, Tools = false },
            ]
            : [],
        DefaultPresent = reachable,
        FallbackPresent = reachable,
    };

    public static AgentSettingsDto Settings(AgentBackendDto backend = AgentBackendDto.Ollama, bool acknowledged = false) => new()
    {
        Backend = backend,
        OllamaModel = "qwen3.5:9b",
        OllamaFallbackModel = "qwen3.5:4b",
        OllamaThink = false,
        ClaudeModel = ClaudeModelDto.ClaudeOpus55,
        ClaudeEffort = ClaudeEffortDto.Medium,
        ClaudeNoticeAcknowledged = acknowledged,
        Limits = AgentLimitsDto.Default,
        UpdatedAt = At,
    };

    public static ConversationSummaryDto Summary(Guid? id = null, string title = "Mix practice", AgentRunStatusDto? last = null) => new()
    {
        ConversationId = id ?? ConversationId,
        Title = title,
        CreatedAt = At,
        UpdatedAt = At,
        LastRunStatus = last,
    };

    public static ConversationDto Conversation(Guid? id = null, IReadOnlyList<AgentMessageDto>? messages = null, IReadOnlyList<AgentRunSummaryDto>? runs = null) => new()
    {
        ConversationId = id ?? ConversationId,
        Title = "Mix practice",
        CreatedAt = At,
        UpdatedAt = At,
        Messages = messages ?? [],
        Runs = runs ?? [],
    };

    public static AgentMessageDto Message(int seq, MessageRoleDto role, string text) => new()
    {
        MessageId = Guid.NewGuid(),
        Seq = seq,
        Role = role,
        Text = text,
        Context = new PageContextDto { Page = "tracks" },
        RunId = RunId,
        CreatedAt = At,
    };

    public static AgentRunSummaryDto RunSummary(AgentRunStatusDto status) => new()
    {
        RunId = RunId,
        MessageId = MessageId,
        Status = status,
        StartedAt = At,
    };

    public static AgentRunDto Run(AgentRunStatusDto status = AgentRunStatusDto.Running) => new()
    {
        RunId = RunId,
        ConversationId = ConversationId,
        Status = status,
        Backend = AgentBackendDto.Ollama,
        Model = "qwen3.5:9b",
        Tainted = false,
        Usage = new TokenUsageDto { InputTokens = 0, OutputTokens = 0, CachedInputTokens = 0 },
        StartedAt = At,
    };

    public static AgentStepDto Step(int seq = 1, string tool = "search_library", AgentStepStatusDto status = AgentStepStatusDto.Running, ToolRiskDto risk = ToolRiskDto.Read) => new()
    {
        StepId = seq == 1 ? StepId : Guid.NewGuid(),
        Seq = seq,
        Tool = tool,
        Kind = ToolKindDto.Server,
        Risk = risk,
        Undo = UndoKindDto.None,
        Arguments = Json("""{"query":"Hero of the Day"}"""),
        ArgumentsHash = "sha256:abc",
        Decision = StepDecisionDto.Auto,
        Status = status,
    };

    public static ApprovalRequestDto Approval(ToolRiskDto risk = ToolRiskDto.Heavy, bool allowPlan = false, IReadOnlyList<string>? reasons = null) => new()
    {
        StepId = StepId,
        Tool = "start_detail_separation",
        Risk = risk,
        Arguments = Json($$"""{"trackId":"{{TrackId}}"}"""),
        ArgumentsHash = "sha256:abc",
        Reasons = reasons ?? [AgentDecisionReasons.RiskHeavy],
        Effect = "Splits Hero of the Day into 6 detail stems",
        Undoable = true,
        EstimatedMinutes = risk == ToolRiskDto.Heavy ? 6.5 : null,
        Device = risk == ToolRiskDto.Heavy ? "cuda" : null,
        AllowPlanScope = allowPlan,
    };
}
