using System.Net;
using System.Text.Json;
using StemStudio.Agent.Backends;
using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.Tests.LyricAssist;

/// <summary>023 T020: LyricModelClient request shapes, model selection and the outbound guard (FR-021, FR-025, FR-026, SC-006).</summary>
public sealed class LyricModelClientTests : IDisposable
{
    private const string Output = """{"items":[],"misses":[]}""";
    private readonly StubHttpHandler _ollama = new();
    private readonly StubHttpHandler _claude = new();
    private readonly GpuSwitch _gpu = new();

    public void Dispose()
    {
        _ollama.Dispose();
        _claude.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LyricModelRequest Request(AssistKindDto kind = AssistKindDto.Check) => new()
    {
        Kind = kind,
        BlockIndex = 0,
        SystemPrompt = "system rules",
        UserPrompt = "<<<SHEET b>>>\n1| line\n<<<END SHEET b>>>",
        Temperature = LyricPromptBuilder.Temperature(kind),
    };

    private LyricModelClient Client(AgentBackendDto backend, FakeEnvironment? environment = null) =>
        new(new InMemorySettingsStore(AgentSettings.Defaults(backend, RunnerHarness.Start) with { ClaudeNoticeAcknowledgedAt = RunnerHarness.Start }), _gpu, new LyricModelBackends
        {
            Ollama = new OllamaChatClientFactory(_ollama.Client()),
            Claude = new ClaudeChatClientFactory((environment ?? FakeEnvironment.WithKey()).Keys(), _claude.Client("https://api.anthropic.com/")),
        });

    private void OllamaAnswers(string content) => _ollama.Json("POST /api/chat", JsonSerializer.Serialize(new
    {
        model = "qwen3.5:9b",
        created_at = "2026-11-17T10:00:00Z",
        message = new { role = "assistant", content },
        done = true,
        done_reason = "stop",
    }));

    [Fact]
    public async Task Ollama_SendsTheSchemaAsFormat_ThinkFalse_TheTemperature_AndTheContextSize()
    {
        OllamaAnswers(Output);

        var reply = await Client(AgentBackendDto.Ollama).CompleteAsync(Request(), Token);

        reply.Json.ShouldBe(Output);
        reply.Model.ShouldBe(new LyricModelInfo { Backend = AgentBackendDto.Ollama, Model = AgentSettings.DefaultOllamaModel, UsingFallback = false });
        reply.Usage.ShouldBeNull();
        using var body = JsonDocument.Parse(_ollama.Requests.Single().Body);
        var root = body.RootElement;
        root.GetProperty("model").GetString().ShouldBe("qwen3.5:9b");
        root.GetProperty("think").GetBoolean().ShouldBeFalse();
        root.GetProperty("format").GetProperty("$id").GetString().ShouldBe("https://stemstudio.local/contracts/lyric-assist-output/1.0.0");
        root.GetProperty("options").GetProperty("num_ctx").GetInt32().ShouldBe(16384);
        root.GetProperty("options").GetProperty("temperature").GetDouble().ShouldBe(0.2, 0.001);
        root.TryGetProperty("tools", out var tools).ShouldBe(tools.ValueKind == JsonValueKind.Array && tools.GetArrayLength() == 0);
        root.GetProperty("messages")[0].GetProperty("role").GetString().ShouldBe("system");
        _claude.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Ollama_WritingHelp_UsesTemperature08_AndAHeavyGpuJobTheCpuFallback()
    {
        OllamaAnswers(Output);
        _gpu.Busy = true;

        var reply = await Client(AgentBackendDto.Ollama).CompleteAsync(Request(AssistKindDto.Alternatives), Token);

        reply.Model.ShouldBe(new LyricModelInfo { Backend = AgentBackendDto.Ollama, Model = AgentSettings.DefaultOllamaFallbackModel, UsingFallback = true });
        using var body = JsonDocument.Parse(_ollama.Requests.Single().Body);
        body.RootElement.GetProperty("model").GetString().ShouldBe("qwen3.5:4b");
        body.RootElement.GetProperty("options").GetProperty("num_gpu").GetInt32().ShouldBe(0);
        body.RootElement.GetProperty("options").GetProperty("temperature").GetDouble().ShouldBe(0.8, 0.001);
    }

    [Fact]
    public async Task Ollama_Unreachable_IsBackendUnavailable()
    {
        _ollama.Failure = new HttpRequestException("connection refused");

        var error = await Should.ThrowAsync<LyricAssistException>(() => Client(AgentBackendDto.Ollama).CompleteAsync(Request(), Token));

        error.Code.ShouldBe(AssistFailureCodeDto.BackendUnavailable);
        error.Fix.ShouldNotBeNull();
    }

    [Fact]
    public async Task Claude_GetsOneForcedToolWithTheSchema_NoOtherTools_AndThinkingOff()
    {
        _claude.Sse("POST /v1/messages", ToolUse("""{"items":[],"misses":[]}"""));

        var reply = await Client(AgentBackendDto.Claude).CompleteAsync(Request() with { MaxOutputTokens = 1234 }, Token);

        JsonDocument.Parse(reply.Json).RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);
        reply.Model.Backend.ShouldBe(AgentBackendDto.Claude);
        reply.Usage.ShouldBe(new TokenUsageDto { InputTokens = 120, OutputTokens = 30, CachedInputTokens = 0 });
        using var body = JsonDocument.Parse(_claude.Requests.Single().Body);
        var root = body.RootElement;
        var tools = root.GetProperty("tools");
        tools.GetArrayLength().ShouldBe(1);
        tools[0].GetProperty("name").GetString().ShouldBe("emit_suggestions");
        tools[0].GetProperty("input_schema").GetProperty("properties").TryGetProperty("items", out _).ShouldBeTrue();
        root.GetProperty("tool_choice").GetProperty("type").GetString().ShouldBe("tool");
        root.GetProperty("tool_choice").GetProperty("name").GetString().ShouldBe("emit_suggestions");
        root.GetProperty("max_tokens").GetInt32().ShouldBe(1234);
        root.TryGetProperty("thinking", out var thinking).ShouldBeTrue();
        thinking.GetProperty("type").GetString().ShouldBe("disabled");
        _ollama.Requests.ShouldBeEmpty();
        _claude.Requests.Single().Headers.ShouldNotContain(FakeEnvironment.SentinelKey[..12] + "x");
    }

    [Fact]
    public async Task Claude_WithoutTheToolCall_IsInvalidOutput()
    {
        _claude.Sse("POST /v1/messages", ClaudeSse.Text("no json"));

        var reply = await Client(AgentBackendDto.Claude).CompleteAsync(Request(), Token);

        reply.Json.ShouldBeEmpty();
        LyricOutputParser.Parse(reply.Json).ShouldBeNull();
    }

    [Fact]
    public async Task Claude_HttpErrors_AreBackendUnavailable_AndAMissingKeyNeverCallsOut()
    {
        _claude.Json("POST /v1/messages", """{"type":"error","error":{"type":"overloaded_error","message":"x"}}""", HttpStatusCode.ServiceUnavailable);

        var failed = await Should.ThrowAsync<LyricAssistException>(() => Client(AgentBackendDto.Claude).CompleteAsync(Request(), Token));
        var noKey = await Should.ThrowAsync<LyricAssistException>(() => Client(AgentBackendDto.Claude, new FakeEnvironment()).CompleteAsync(Request(), Token));

        failed.Code.ShouldBe(AssistFailureCodeDto.BackendUnavailable);
        noKey.Code.ShouldBe(AssistFailureCodeDto.BackendUnavailable);
        _claude.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(AgentBackendDto.None)]
    [InlineData(AgentBackendDto.Fake)]
    public async Task NoModelBackend_MakesNoRequestAtAll(AgentBackendDto backend)
    {
        var error = await Should.ThrowAsync<LyricAssistException>(() => Client(backend).CompleteAsync(Request(), Token));

        error.Code.ShouldBe(AssistFailureCodeDto.BackendUnavailable);
        _ollama.Requests.ShouldBeEmpty();
        _claude.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASwitchToClaudeMidAssist_IsRefused_WithoutAnyClaudeRequest()
    {
        var store = new InMemorySettingsStore(AgentSettings.Defaults(AgentBackendDto.Ollama, RunnerHarness.Start) with { ClaudeNoticeAcknowledgedAt = RunnerHarness.Start });
        var client = new LyricModelClient(store, _gpu, new LyricModelBackends
        {
            Ollama = new OllamaChatClientFactory(_ollama.Client()),
            Claude = new ClaudeChatClientFactory(FakeEnvironment.WithKey().Keys(), _claude.Client("https://api.anthropic.com/")),
        });
        OllamaAnswers(Output);
        var pinned = await client.SelectAsync(Token);
        await client.CompleteAsync(Request() with { Model = pinned }, Token);

        await store.SaveAsync(store.Current with { Backend = AgentBackendDto.Claude }, Token);
        var error = await Should.ThrowAsync<LyricAssistException>(() => client.CompleteAsync(Request() with { Model = pinned }, Token));

        error.Code.ShouldBe(AssistFailureCodeDto.BackendUnavailable);
        error.Message.ShouldBe(LyricModelClient.BackendChanged);
        _claude.Requests.ShouldBeEmpty();
        _ollama.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TheFallbackModel_MayTakeOverMidAssist_OnTheSameBackend()
    {
        OllamaAnswers(Output);
        var client = Client(AgentBackendDto.Ollama);
        var pinned = await client.SelectAsync(Token);
        _gpu.Busy = true;

        var reply = await client.CompleteAsync(Request() with { Model = pinned }, Token);

        reply.Model.UsingFallback.ShouldBeTrue();
    }

    [Fact]
    public void Options_AreBuiltWithoutAnyRequest()
    {
        LyricModelClient.ClaudeOptions(Request()).MaxOutputTokens.ShouldBe(LyricAssistLimits.DefaultMaxOutputTokensPerAssist);
        LyricModelClient.OllamaOptions(Request(), cpuOnly: false).ResponseFormat.ShouldNotBeNull();
        Should.Throw<ArgumentNullException>(() => LyricModelClient.ClaudeOptions(null!));
        Should.Throw<ArgumentNullException>(() => LyricModelClient.OllamaOptions(null!, false));
    }

    private static string ToolUse(string input) => string.Join("\n\n",
        Event("message_start", """{"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":120,"output_tokens":1}}}"""),
        Event("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"emit_suggestions","input":{}}}"""),
        Event("content_block_delta", $$$"""{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":{{{JsonSerializer.Serialize(input)}}}}}"""),
        Event("content_block_stop", """{"type":"content_block_stop","index":0}"""),
        Event("message_delta", """{"type":"message_delta","delta":{"stop_reason":"tool_use","stop_sequence":null},"usage":{"output_tokens":30}}"""),
        Event("message_stop", """{"type":"message_stop"}""")) + "\n\n";

    private static string Event(string name, string data) => $"event: {name}\ndata: {data}";

    private sealed class GpuSwitch : IGpuActivity
    {
        public bool Busy { get; set; }

        public bool IsHeavyJobActive() => Busy;
    }
}
