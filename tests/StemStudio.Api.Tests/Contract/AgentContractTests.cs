using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, feature 011 Phase 0, T003/T005): the agent DTOs and routes against backend-api 1.10.0
/// (<c>specs/011-ai-composer-agent/contracts/backend-api.openapi.yaml</c>), and the merge rule: 1.10.0 contains 010's 1.9.0
/// unchanged and the standalone delta <c>agent-api.openapi.yaml</c> (1.10.0-delta.1) unchanged, except the four components the
/// delta shares with 1.9.0, which keep their 1.9.0 definitions.
/// </summary>
public sealed class AgentContractTests
{
    private static readonly Lazy<OpenApiContract> Delta =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "011", "agent-api.openapi.yaml")));

    private static readonly Guid Id = Guid.Parse("0b9d3c6e-2f4a-4c8b-9e1d-7a6b5c4d3e2f");
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly string Hash = new('a', 64);

    // Components the delta defines like 1.9.0 (same schemas; the two responses differ only in description or example text).
    private static readonly string[] SharedWith190 = ["ValidationProblem", "NotFound", "ProblemDetails", "ValidationProblemDetails"];

    public static TheoryData<string> Operations => new(
        "getAgentStatus", "getAgentSettings", "updateAgentSettings", "testAgentBackend", "listAgentTools", "listConversations",
        "createConversation", "getConversation", "renameConversation", "deleteConversation", "sendAgentMessage",
        "undoLastAgentAction", "getAgentRun", "streamAgentRunEvents", "decideAgentApproval", "submitAgentUiResult", "cancelAgentRun");

    [Fact]
    public void Delta_IsTheAdditive1100DraftWithOnlyAgentPaths()
    {
        Delta.Value.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.10.0-delta.1");
        Delta.Value.Operations.ShouldAllBe(op => op.Path.StartsWith(ApiRoutes.Agent + "/", StringComparison.Ordinal));
        Delta.Value.Operations.Select(op => op.Operation["operationId"]!.GetValue<string>()).ShouldBe(Operations.Select(row => row.Data), ignoreOrder: true);
    }

    [Fact]
    public void Delta_CollidesWithNothingInBackend190()
    {
        var existing = Backend010.Operations.Select(op => op.Operation["operationId"]!.GetValue<string>()).ToHashSet();

        Delta.Value.Operations.ShouldAllBe(op => !existing.Contains(op.Operation["operationId"]!.GetValue<string>()));
        Section(Delta.Value.Root, "paths").Select(p => p.Key).ShouldAllBe(path => !Section(Backend010.Root, "paths").ContainsKey(path));
    }

    [Fact]
    public void Backend1100_Is190PlusExactlyTheDeltaOperations()
    {
        var expected = Backend010.Operations.Concat(Delta.Value.Operations).Select(Signature);

        OpenApiContract.Backend.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.10.0");
        OpenApiContract.Backend.Operations.Select(Signature).ShouldBe(expected, ignoreOrder: true);
        OpenApiContract.Backend.Root["tags"]!.AsArray().Select(tag => tag!["name"]!.GetValue<string>()).ShouldContain("agent");
    }

    [Theory]
    [MemberData(nameof(Sections), MemberType = typeof(ContractJson))]
    public void Backend1100_Contains190Unchanged(string section)
    {
        ChangedEntries(Backend010, OpenApiContract.Backend, section).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("paths")]
    [InlineData("components/parameters")]
    [InlineData("components/responses")]
    [InlineData("components/schemas")]
    public void Backend1100_ContainsTheDeltaUnchangedExceptTheShared190Components(string section)
    {
        var merged = Section(OpenApiContract.Backend.Root, section);

        ChangedEntries(Delta.Value, OpenApiContract.Backend, section).ShouldBeSubsetOf(SharedWith190);
        Section(Delta.Value.Root, section).Select(entry => entry.Key).Where(SharedWith190.Contains)
            .ShouldAllBe(key => JsonNode.DeepEquals(Section(Backend010.Root, section)[key], merged[key]));
    }

    [Theory]
    [InlineData(typeof(AgentBackendDto), "AgentBackend")]
    [InlineData(typeof(ClaudeModelDto), "ClaudeModel")]
    [InlineData(typeof(ClaudeEffortDto), "ClaudeEffort")]
    [InlineData(typeof(AgentRunStatusDto), "AgentRunStatus")]
    [InlineData(typeof(AgentStepStatusDto), "AgentStepStatus")]
    [InlineData(typeof(StepDecisionDto), "StepDecision")]
    [InlineData(typeof(ToolKindDto), "ToolKind")]
    [InlineData(typeof(ToolRiskDto), "ToolRisk")]
    [InlineData(typeof(UndoKindDto), "UndoKind")]
    [InlineData(typeof(PlanItemStatusDto), "PlanItemStatus")]
    [InlineData(typeof(ApprovalDecisionDto), "ApprovalDecision")]
    [InlineData(typeof(ApprovalScopeDto), "ApprovalScope")]
    [InlineData(typeof(ApprovalStatusDto), "ApprovalStatus")]
    [InlineData(typeof(MessageRoleDto), "MessageRole")]
    [InlineData(typeof(JobPhaseDto), "JobPhase")]
    public void Enums_MatchTheContract(Type enumType, string schema)
    {
        WireNames(enumType).ShouldBe(Strings(AgentSchema(schema)["enum"]!));
    }

    [Fact]
    public void Constants_MatchTheContractEnums()
    {
        Strings(AgentSchema("DecisionReason")["enum"]!).ShouldBe(ConstantValues(typeof(AgentDecisionReasons)));
        Strings(AgentSchema("RunFailure")["properties"]!["code"]!["enum"]!).ShouldBe(ConstantValues(typeof(AgentFailureCodes)));
        var mapping = AgentSchema("AgentRunEvent")["discriminator"]!["mapping"]!.AsObject().Select(entry => entry.Key);
        mapping.ShouldBe(AgentEventTypes.All);
    }

    [Fact]
    public void ApprovalDevice_IsAProcessingDeviceCodeOrNull()
    {
        var device = AgentSchema("ApprovalRequest")["properties"]!["device"]!;

        var values = device["enum"]!.AsArray().ToList();

        values.Where(value => value is not null).Select(value => value!.GetValue<string>()).ShouldBe(Strings(AgentSchema("ProcessingDeviceKind")["enum"]!));
        values.ShouldContain(value => value == null);
    }

    [Theory]
    [InlineData("run.completed", true)]
    [InlineData("run.failed", true)]
    [InlineData("run.cancelled", true)]
    [InlineData("run.interrupted", true)]
    [InlineData("text.delta", false)]
    [InlineData("approval.requested", false)]
    public void EventTypes_TerminalOnesCloseTheStream(string eventType, bool terminal)
    {
        AgentEventTypes.IsTerminal(eventType).ShouldBe(terminal);
    }

    [Fact]
    public void ProblemTypes_AreStemStudioUris()
    {
        AgentProblemTypes.Uri(AgentProblemTypes.RunActive).ShouldBe("https://stemstudio.local/problems/run-active");
    }

    [Fact]
    public void Routes_ExistInTheContract()
    {
        string[] routes =
        [
            ApiRoutes.AgentStatus, ApiRoutes.AgentSettings, ApiRoutes.AgentSettingsTest, ApiRoutes.AgentTools, ApiRoutes.AgentConversations,
            ApiRoutes.AgentConversation(Id), ApiRoutes.AgentMessages(Id), ApiRoutes.AgentUndo(Id), ApiRoutes.AgentRun(Id),
            ApiRoutes.AgentRunEvents(Id), ApiRoutes.AgentApproval(Id, Id), ApiRoutes.AgentUiResult(Id, Id), ApiRoutes.AgentCancel(Id),
        ];
        var templates = Section(OpenApiContract.Backend.Root, "paths").Select(p => p.Key)
            .Where(path => path.StartsWith(ApiRoutes.Agent + "/", StringComparison.Ordinal)).Select(key => key.Replace("{conversationId}", $"{Id}").Replace("{runId}", $"{Id}").Replace("{stepId}", $"{Id}")).ToList();

        routes.ShouldBe(templates, ignoreOrder: true);
        ApiRoutes.AgentRunEvents(Id, 7).ShouldBe($"/api/agent/runs/{Id}/events?after=7");
    }

    [Fact]
    public void LimitsDefault_MatchesTheSchemaDefaults()
    {
        var properties = AgentSchema("AgentLimits")["properties"]!.AsObject();
        var defaults = JsonSerializer.SerializeToNode(AgentLimitsDto.Default, Web)!.AsObject();

        defaults.Select(p => p.Key).ShouldBe(properties.Select(p => p.Key));
        // The YAML loader reads numbers as double.
        defaults.ShouldAllBe(p => p.Value!.GetValue<int>() == properties[p.Key]!["default"]!.GetValue<double>());
    }

    [Theory]
    [MemberData(nameof(Responses))]
    [MemberData(nameof(Requests))]
    public void Dtos_ValidateAgainstTheirSchemas(string schema, object dto)
    {
        var json = JsonSerializer.SerializeToNode(dto, dto.GetType(), Web);

        SchemaValidator.Validate(OpenApiContract.Backend, AgentSchema(schema), json).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Responses))]
    [MemberData(nameof(Requests))]
    public void Dtos_WriteExactlyTheSchemaProperties(string schema, object dto)
    {
        var written = JsonSerializer.SerializeToNode(dto, dto.GetType(), Web)!.AsObject().Select(p => p.Key);

        written.ShouldBe(SchemaProperties(AgentSchema(schema)), ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(RunEvents))]
    [MemberData(nameof(StepEvents))]
    public void Events_ValidateAsDecodedSseEvents(string eventType, object data)
    {
        var envelope = new JsonObject { ["id"] = 3, ["event"] = eventType, ["data"] = JsonSerializer.SerializeToNode(data, data.GetType(), Web) };

        SchemaValidator.Validate(OpenApiContract.Backend, AgentSchema("AgentRunEvent"), envelope).ShouldBeEmpty();
    }

    [Fact]
    public void Requests_RoundTripFromTheWire()
    {
        var json = """{"decision":"approve","scope":"plan_heavy","argumentsHash":"HASH"}""".Replace("HASH", Hash, StringComparison.Ordinal);

        var request = JsonSerializer.Deserialize<DecideApprovalRequest>(json, Web)!;

        (request.Decision, request.Scope, request.ArgumentsHash).ShouldBe((ApprovalDecisionDto.Approve, ApprovalScopeDto.PlanHeavy, Hash));
        JsonSerializer.Deserialize<UpdateAgentSettingsRequest>("{}", Web)!.AcknowledgeClaudeNotice.ShouldBeFalse();
    }

    public static TheoryData<string, object> Responses => new()
    {
        { "AgentStatus", Status() },
        { "AgentToolInfo", new AgentToolInfoDto { Name = "mixer_set_channel", Feature = "001", Kind = ToolKindDto.Ui, Risk = ToolRiskDto.Change, Undo = UndoKindDto.Inverse, Available = false, UnavailableReason = "No mixer is open.", Title = "Set a channel", Description = "Changes one channel." } },
        { "BackendTestResult", new BackendTestResultDto { Backend = AgentBackendDto.Ollama, Ok = true, LatencyMs = 12, Detail = "Ollama 0.40.16" } },
        { "AgentSettings", Settings() },
        { "ConversationSummary", new ConversationSummaryDto { ConversationId = Id, Title = "Hero", CreatedAt = Now, UpdatedAt = Now, LastRunStatus = AgentRunStatusDto.Completed } },
        { "Conversation", Conversation() },
        { "AgentMessageAccepted", new AgentMessageAcceptedDto { RunId = Id, MessageId = Id } },
        { "AgentRunAccepted", new AgentRunAcceptedDto { RunId = Id } },
        { "AgentRun", Run() },
        { "ApprovalState", Approved() },
    };

    public static TheoryData<string, object> Requests => new()
    {
        { "UpdateAgentSettingsRequest", new UpdateAgentSettingsRequest { Backend = AgentBackendDto.Claude, ClaudeModel = ClaudeModelDto.ClaudeSonnet55, ClaudeEffort = ClaudeEffortDto.Medium, OllamaModel = "qwen3.5:9b", OllamaFallbackModel = "qwen3.5:4b", OllamaThink = true, Limits = AgentLimitsDto.Default, AcknowledgeClaudeNotice = true } },
        { "TestAgentBackendRequest", new TestAgentBackendRequest { Backend = AgentBackendDto.Ollama } },
        { "CreateConversationRequest", new CreateConversationRequest { Title = "Practice" } },
        { "RenameConversationRequest", new RenameConversationRequest { Title = "Practice" } },
        { "SendAgentMessageRequest", new SendAgentMessageRequest { Text = "Mute the drums", Context = Context() } },
        { "DecideApprovalRequest", new DecideApprovalRequest { Decision = ApprovalDecisionDto.Reject, Scope = ApprovalScopeDto.Once, ArgumentsHash = Hash } },
        { "SubmitUiResultRequest", new SubmitUiResultRequest { Ok = false, Result = Object("""{"stem":"drums"}"""), Error = "The mixer of Jolene is not open" } },
        { "CancelAgentRunRequest", new CancelAgentRunRequest { CancelJobs = true } },
    };

    public static TheoryData<string, object> RunEvents => new()
    {
        { AgentEventTypes.RunStarted, new RunStartedEventDto { RunId = Id, Backend = AgentBackendDto.Ollama, Model = "qwen3.5:9b", UsingFallback = false } },
        { AgentEventTypes.ModelChanged, new ModelChangedEventDto { Model = "qwen3.5:4b", UsingFallback = true, Reason = "A GPU job started" } },
        { AgentEventTypes.PlanUpdated, new PlanUpdatedEventDto { Plan = [Plan()] } },
        { AgentEventTypes.TextDelta, new TextDeltaEventDto { Text = "Muting" } },
        { AgentEventTypes.UsageUpdated, new TokenUsageDto { InputTokens = 1200, OutputTokens = 40, CachedInputTokens = 1000 } },
        { AgentEventTypes.RunCompleted, new RunCompletedEventDto { RunId = Id, MessageId = Id } },
        { AgentEventTypes.RunFailed, new RunFailureDto { Code = AgentFailureCodes.Refusal, Message = "Claude declined the request.", Fix = "Switch to local" } },
        { AgentEventTypes.RunCancelled, new RunCancelledEventDto { RunId = Id, CancelledJobs = [$"{Id}"] } },
        { AgentEventTypes.RunInterrupted, new RunInterruptedEventDto { RunId = Id } },
    };

    public static TheoryData<string, object> StepEvents => new()
    {
        { AgentEventTypes.StepCreated, Step() },
        { AgentEventTypes.StepUpdated, new StepUpdatedEventDto { StepId = Id, Status = AgentStepStatusDto.WaitingJob, Progress = 40 } },
        { AgentEventTypes.ApprovalRequested, Approval() },
        { AgentEventTypes.ApprovalResolved, Approved() },
        { AgentEventTypes.UiRequested, new UiRequestedEventDto { StepId = Id, Tool = "mixer_set_channel", Arguments = Object("""{"mute":true,"stem":"drums"}"""), TimeoutSeconds = AgentContractLimits.UiToolTimeoutSeconds } },
    };

    private static JsonNode AgentSchema(string name) => OpenApiContract.Backend.Root["components"]!["schemas"]![name]!;

    private static string Signature((string Path, string Method, JsonObject Operation) op) =>
        $"{op.Method} {op.Path} {op.Operation["operationId"]!.GetValue<string>()}";

    private static IEnumerable<string> SchemaProperties(JsonNode schema)
    {
        var resolved = Delta.Value.Resolve(schema);
        var parts = resolved["allOf"]?.AsArray().Select(part => Delta.Value.Resolve(part!)) ?? [resolved];
        return parts.SelectMany(part => part["properties"]!.AsObject().Select(p => p.Key));
    }

    private static IEnumerable<string> ConstantValues(Type type) =>
        type.GetFields().Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!);

    private static JsonElement Object(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static PageContextDto Context() => new() { Page = "mixer", TrackId = Id, MixId = null };

    private static PlanItemDto Plan() => new() { Index = 0, Title = "Mute the drums", Tool = "mixer_set_channel", Status = PlanItemStatusDto.Done };

    private static ApprovalStateDto Approved() => new() { StepId = Id, Status = ApprovalStatusDto.Approved, Scope = ApprovalScopeDto.Once, DecidedAt = Now };

    private static AgentStatusDto Status() => new()
    {
        Backend = AgentBackendDto.Ollama,
        Ready = false,
        UsingFallback = false,
        Reason = "qwen3.5:9b is not installed",
        Fix = "ollama pull qwen3.5:9b (6.6 GB)",
        Ollama = new OllamaStatusDto { Reachable = true, Version = "0.40.16", Models = [new OllamaModelInfoDto { Name = "qwen3.5:4b", SizeBytes = 3_400_000_000, Tools = true }], DefaultPresent = false, FallbackPresent = true },
        Claude = new ClaudeStatusDto { KeyConfigured = false, NoticeAcknowledged = false, Model = ClaudeModelDto.ClaudeOpus55 },
    };

    private static AgentSettingsDto Settings() => new()
    {
        Backend = AgentBackendDto.Ollama,
        OllamaModel = "qwen3.5:9b",
        OllamaFallbackModel = "qwen3.5:4b",
        OllamaThink = false,
        ClaudeModel = ClaudeModelDto.ClaudeOpus55,
        ClaudeEffort = ClaudeEffortDto.Low,
        ClaudeNoticeAcknowledged = false,
        Limits = AgentLimitsDto.Default,
        UpdatedAt = Now,
    };

    private static ConversationDto Conversation() => new()
    {
        ConversationId = Id,
        Title = "Hero",
        CreatedAt = Now,
        UpdatedAt = Now,
        Messages = [new AgentMessageDto { MessageId = Id, Seq = 1, Role = MessageRoleDto.User, Text = "Mute the drums", Context = Context(), RunId = Id, CreatedAt = Now }],
        Runs = [new AgentRunSummaryDto { RunId = Id, MessageId = Id, Status = AgentRunStatusDto.Running, StartedAt = Now }],
    };

    private static AgentStepDto Step() => new()
    {
        StepId = Id,
        Seq = 1,
        Tool = "start_detail_separation",
        Kind = ToolKindDto.Server,
        Risk = ToolRiskDto.Heavy,
        Undo = UndoKindDto.Inverse,
        Arguments = Object($$"""{"trackId":"{{Id}}"}"""),
        ArgumentsHash = Hash,
        Decision = StepDecisionDto.ApprovalRequired,
        DecisionReasons = [AgentDecisionReasons.RiskHeavy],
        Status = AgentStepStatusDto.AwaitingApproval,
        Job = new JobRefDto { TrackId = Id, Phase = JobPhaseDto.Detail },
    };

    private static ApprovalRequestDto Approval() => new()
    {
        StepId = Id,
        Tool = "start_detail_separation",
        Risk = ToolRiskDto.Heavy,
        Arguments = Object($$"""{"trackId":"{{Id}}"}"""),
        ArgumentsHash = Hash,
        Reasons = [AgentDecisionReasons.RiskHeavy],
        Effect = "Splits Enter Sandman into detail stems",
        Undoable = true,
        EstimatedMinutes = 4,
        Device = "gpu",
        AllowPlanScope = true,
    };

    private static AgentRunDto Run() => new()
    {
        RunId = Id,
        ConversationId = Id,
        Status = AgentRunStatusDto.AwaitingApproval,
        Backend = AgentBackendDto.Ollama,
        Model = "qwen3.5:9b",
        Tainted = false,
        Plan = [Plan()],
        Steps = [Step(), Step() with { Seq = 2, Status = AgentStepStatusDto.Succeeded, ResultSummary = Object("""{"ok":true}"""), Job = null }],
        PendingApproval = Approval(),
        Usage = new TokenUsageDto { InputTokens = 0, OutputTokens = 0, CachedInputTokens = 0 },
        Failure = null,
        StartedAt = Now,
    };
}
