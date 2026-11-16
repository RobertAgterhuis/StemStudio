using System.Net;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Agent.Tests.Backends;

/// <summary>
/// T064: the optional Claude backend against a stub handler for api.anthropic.com (no request leaves the process, no real key):
/// key only from the injected environment reader, model/effort/tool mode/streaming/max tokens, per-run token budget, refusal
/// and HTTP errors as readable failures, probe without requests, test only on request (FR-018, FR-020, SC-006).
/// </summary>
public sealed class ClaudeBackendTests
{
    private const string Messages = "POST /v1/messages";
    private readonly FakeTimeProvider _time = new(RunnerHarness.Start);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AgentSettings Allowed(ClaudeModelDto model = ClaudeModelDto.ClaudeOpus55, ClaudeEffortDto effort = ClaudeEffortDto.Low) =>
        AgentSettings.Defaults(AgentBackendDto.Claude, RunnerHarness.Start) with { ClaudeModel = model, ClaudeEffort = effort, ClaudeNoticeAcknowledgedAt = RunnerHarness.Start };

    private static string Stream(string text, string stopReason = "end_turn", int input = 120, int output = 7) => string.Join("\n\n",
        Event("message_start", $$$$"""{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":{{{{input}}}},"output_tokens":1}}}"""),
        Event("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}"""),
        Event("content_block_delta", $$$"""{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"{{{text}}}"}}"""),
        Event("content_block_stop", """{"type":"content_block_stop","index":0}"""),
        Event("message_delta", $$$"""{"type":"message_delta","delta":{"stop_reason":"{{{stopReason}}}","stop_sequence":null},"usage":{"output_tokens":{{{output}}}}}"""),
        Event("message_stop", """{"type":"message_stop"}""")) + "\n\n";

    private static string Event(string name, string data) => $"event: {name}\ndata: {data}";

    private static StubHttpHandler Claude() => new();

    private static ClaudeChatClientFactory Factory(StubHttpHandler stub, IEnvironmentReader? environment = null) =>
        new(new ClaudeKeyProvider(environment ?? FakeEnvironment.WithKey(), new FakeStoredClaudeKeyReader()), stub.Client("https://api.anthropic.com/"));

    private static JsonElement LastBody(StubHttpHandler stub) => JsonDocument.Parse(stub.Requests.Last(request => request.Route == Messages).Body).RootElement;

    [Fact]
    public async Task Session_SendsModelEffortAdaptiveThinkingAutoToolModeStreamingAndMaxTokens_WithTheKeyFromTheReader()
    {
        var stub = Claude().Sse(Messages, Stream("Hallo"));
        var environment = FakeEnvironment.WithKey();
        var session = new ClaudeChatSession(new InMemorySettingsStore(Allowed(effort: ClaudeEffortDto.High)), Factory(stub, environment));
        var turn = await session.NextTurnAsync(Context(AgentBackendDto.Claude), Token);
        var options = turn.Options!.Clone();
        options.ToolMode = ChatToolMode.Auto;
        options.Tools = [AIFunctionFactory.Create(() => "ok", "echo", "Echo.")];

        var updates = await turn.Client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")], options, Token).ToListAsync(Token);

        updates.ToChatResponse().Text.ShouldBe("Hallo");
        (turn.Model, turn.UsingFallback, turn.TimeoutSeconds).ShouldBe(("claude-opus-5-5", false, 180));
        var body = LastBody(stub);
        body.GetProperty("model").GetString().ShouldBe("claude-opus-5-5");
        body.GetProperty("stream").GetBoolean().ShouldBeTrue();
        body.GetProperty("max_tokens").GetInt32().ShouldBe(8000);
        body.GetProperty("output_config").GetProperty("effort").GetString().ShouldBe("high");
        body.GetProperty("thinking").GetProperty("type").GetString().ShouldBe("adaptive");
        body.GetProperty("tool_choice").GetProperty("type").GetString().ShouldBe("auto");
        stub.Requests.Single().Headers.ShouldContain(FakeEnvironment.SentinelKey, Case.Sensitive);
        stub.Requests.Single().Headers.ShouldContain("x-stainless-retry-count: 0");
        environment.Reads.ShouldAllBe(name => name == ClaudeChatClientFactory.KeyVariable);
    }

    [Theory]
    [InlineData(ClaudeModelDto.ClaudeSonnet55, "claude-sonnet-5-5")]
    [InlineData(ClaudeModelDto.ClaudeHaiku55, "claude-haiku-5-5")]
    public void ModelIds_FollowTheSetting(ClaudeModelDto model, string id) => ClaudeChatClientFactory.ModelId(model).ShouldBe(id);

    [Fact]
    public async Task Session_MaxTokensShrinkToTheRemainingOutputBudget()
    {
        var stub = Claude().Sse(Messages, Stream("ok"));
        var session = new ClaudeChatSession(new InMemorySettingsStore(Allowed()), Factory(stub));

        var turn = await session.NextTurnAsync(Context(AgentBackendDto.Claude, new TokenUsage(1000, 7_000, 0)), Token);

        turn.Options!.MaxOutputTokens.ShouldBe(1000);
    }

    [Theory]
    [InlineData(60_000, 10)]
    [InlineData(10, 8_000)]
    public async Task Runner_StopsWithBudgetExhausted_WithoutCallingClaude(long input, long output)
    {
        var stub = Claude().Sse(Messages, Stream("never"));
        var harness = ClaudeHarness(stub, new TokenUsage(input, output, 0));

        var run = await harness.ExecuteAsync();

        (run.Status, run.FailureCode).ShouldBe((AgentRunStatusDto.Failed, AgentFailureCodes.BudgetExhausted));
        run.FailureMessage!.ShouldContain("Switch to local");
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Runner_CompletesWithClaude_AndPublishesUsage()
    {
        var stub = Claude().Sse(Messages, Stream("Klaar"));
        var harness = ClaudeHarness(stub);

        var run = await harness.ExecuteAsync();

        run.Status.ShouldBe(AgentRunStatusDto.Completed);
        (run.Usage.InputTokens, run.Usage.OutputTokens).ShouldBe((120L, 7L));
        harness.Events.Types.ShouldContain(AgentEventTypes.UsageUpdated);
    }

    [Fact]
    public async Task Runner_Refusal_FailsReadably()
    {
        var stub = Claude().Sse(Messages, Stream("", "refusal"));
        var harness = ClaudeHarness(stub);

        var run = await harness.ExecuteAsync();

        (run.Status, run.FailureCode).ShouldBe((AgentRunStatusDto.Failed, AgentFailureCodes.Refusal));
        run.FailureMessage.ShouldBe(ClaudeErrors.Refusal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "401")]
    [InlineData(HttpStatusCode.Forbidden, "403")]
    [InlineData(HttpStatusCode.TooManyRequests, "429")]
    [InlineData(HttpStatusCode.InternalServerError, "500")]
    [InlineData((HttpStatusCode)529, "529")]
    [InlineData(HttpStatusCode.BadRequest, "400")]
    public async Task Runner_HttpErrors_FailReadably_AndNeverLeakTheKey(HttpStatusCode status, string code)
    {
        var stub = Claude().Json(Messages, $$$"""{"type":"error","error":{"type":"x","message":"echo {{{FakeEnvironment.SentinelKey}}}"}}""", status);
        var logger = new CapturingLogger();
        var harness = ClaudeHarness(stub, logger: logger);

        var run = await harness.ExecuteAsync();

        (run.Status, run.FailureCode).ShouldBe((AgentRunStatusDto.Failed, AgentFailureCodes.BackendUnavailable));
        run.FailureMessage!.ShouldContain(code);
        run.FailureMessage!.ShouldContain("Switch to local");
        AssertNoKey(harness, logger, run);
    }

    [Fact]
    public async Task Runner_UnreachableClaude_FailsReadably()
    {
        var stub = new StubHttpHandler { Failure = new HttpRequestException("no route") };

        var run = await ClaudeHarness(stub).ExecuteAsync();

        (run.Status, run.FailureCode, run.FailureMessage).ShouldBe((AgentRunStatusDto.Failed, AgentFailureCodes.BackendUnavailable, ClaudeErrors.Unreachable));
    }

    [Fact]
    public async Task Session_WithoutKeyOrNotice_IsUnavailable_AndSendsNothing()
    {
        var stub = MustNotBeCalled();
        var noKey = new ClaudeChatSession(new InMemorySettingsStore(Allowed()), Factory(stub, new FakeEnvironment(("ANTHROPIC_API_KEY", "  "))));
        var noNotice = new ClaudeChatSession(new InMemorySettingsStore(Allowed() with { ClaudeNoticeAcknowledgedAt = null }), Factory(stub));

        var missing = await Should.ThrowAsync<BackendUnavailableException>(() => noKey.NextTurnAsync(Context(AgentBackendDto.Claude), Token));
        var unacknowledged = await Should.ThrowAsync<BackendUnavailableException>(() => noNotice.NextTurnAsync(Context(AgentBackendDto.Claude), Token));

        missing.Message.ShouldBe(ClaudeErrors.KeyMissing);
        unacknowledged.Message.ShouldBe(ClaudeErrors.NoticeMissing);
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Probe_ReportsOnlyKeyPresence_WithoutAnyRequest()
    {
        var stub = MustNotBeCalled();

        var configured = new ClaudeBackendProbe(Factory(stub), _time).Status(Allowed(ClaudeModelDto.ClaudeHaiku55));
        var missing = new ClaudeBackendProbe(Factory(stub, new FakeEnvironment()), _time).Status(Allowed() with { ClaudeNoticeAcknowledgedAt = null });

        (configured.KeyConfigured, configured.NoticeAcknowledged, configured.Model).ShouldBe((true, true, ClaudeModelDto.ClaudeHaiku55));
        (missing.KeyConfigured, missing.NoticeAcknowledged).ShouldBe((false, false));
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Test_LooksUpTheModel_OnlyOnRequest()
    {
        var stub = Claude().Json("GET /v1/models/claude-opus-5-5", """{"type":"model","id":"claude-opus-5-5","display_name":"Claude Opus 5.5","created_at":"2026-10-01T00:00:00Z"}""");

        var result = await new ClaudeBackendProbe(Factory(stub), _time).TestAsync(Allowed(), Token);

        (result.Backend, result.Ok).ShouldBe((AgentBackendDto.Claude, true));
        result.Detail.ShouldBe("Claude answered; model claude-opus-5-5 is available.");
        stub.Requests.Select(request => request.Route).ShouldBe(["GET /v1/models/claude-opus-5-5"]);
        result.Detail.ShouldNotContain(FakeEnvironment.SentinelKey);
    }

    [Fact]
    public async Task Test_RejectedKey_IsNotOk_AndNeverEchoesTheKey()
    {
        var stub = Claude().Json("GET /v1/models/claude-opus-5-5", $$$"""{"type":"error","error":{"type":"authentication_error","message":"{{{FakeEnvironment.SentinelKey}}}"}}""", HttpStatusCode.Unauthorized);

        var result = await new ClaudeBackendProbe(Factory(stub), _time).TestAsync(Allowed(), Token);

        result.Ok.ShouldBeFalse();
        result.Detail.ShouldContain("401");
        result.Detail.ShouldNotContain(FakeEnvironment.SentinelKey);
    }

    [Fact]
    public async Task Test_WithoutKey_SendsNothing()
    {
        var stub = MustNotBeCalled();

        var result = await new ClaudeBackendProbe(Factory(stub, new FakeEnvironment()), _time).TestAsync(Allowed(), Token);

        (result.Ok, result.Detail).ShouldBe((false, ClaudeErrors.KeyMissing));
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Selector_RoutesClaudeRunsToTheClaudeSession_AndOllamaRunsNeverTouchClaude()
    {
        var claude = MustNotBeCalled();
        var selector = new AgentModelSelector(new InMemorySettingsStore(Allowed()), Substitute.For<IGpuActivity>(), new ChatBackends
        {
            Ollama = new OllamaChatClientFactory(new StubHttpHandler().Client()),
            Claude = new ClaudeChatSession(new InMemorySettingsStore(Allowed()), Factory(claude)),
        });

        var ollama = await selector.NextTurnAsync(Context(AgentBackendDto.Ollama), Token);
        var paid = await selector.NextTurnAsync(Context(AgentBackendDto.Claude), Token);

        ollama.Model.ShouldBe(AgentSettings.DefaultOllamaModel);
        paid.Model.ShouldBe("claude-opus-5-5");
        claude.Requests.ShouldBeEmpty();
    }

    private static StubHttpHandler MustNotBeCalled() => new() { Failure = new InvalidOperationException("Claude must not be called.") };

    private static ChatTurnContext Context(AgentBackendDto backend, TokenUsage? usage = null) =>
        new(Run(backend) with { Usage = usage ?? TokenUsage.Zero }, new PageContextDto { Page = "tracks" });

    private static AgentRun Run(AgentBackendDto backend) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserMessageId = Guid.NewGuid(),
        Status = AgentRunStatusDto.Running,
        Backend = backend,
        Model = "m",
        Tainted = false,
        ModelTurns = 0,
        ToolCalls = 0,
        InvalidCalls = 0,
        StartedAt = RunnerHarness.Start,
    };

    private static RunnerHarness ClaudeHarness(StubHttpHandler stub, TokenUsage? usage = null, CapturingLogger? logger = null)
    {
        var harness = new RunnerHarness(new ThrowingChatClient(new InvalidOperationException("unused")), [TestTools.Echo()]) { Logger = logger };
        harness.Settings.SaveAsync(Allowed(), CancellationToken.None);
        harness.UseBackend(AgentBackendDto.Claude, usage);
        harness.UseSession(new ClaudeChatSession(harness.Settings, Factory(stub)));
        return harness;
    }

    private static void AssertNoKey(RunnerHarness harness, CapturingLogger logger, AgentRun run)
    {
        harness.Events.Events.ShouldAllBe(item => !item.Payload.Contains(FakeEnvironment.SentinelKey, StringComparison.Ordinal));
        logger.Entries.ShouldAllBe(entry => !entry.Text.Contains(FakeEnvironment.SentinelKey, StringComparison.Ordinal));
        (run.FailureMessage ?? string.Empty).ShouldNotContain(FakeEnvironment.SentinelKey);
    }
}
