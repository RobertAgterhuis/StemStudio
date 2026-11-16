using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Backends.Fakes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests.Backends;

/// <summary>T050: Ollama factory options, probe, GPU release and the model selector, all against stub handlers (FR-018, FR-021..FR-023).</summary>
public sealed class OllamaBackendTests
{
    private const string Done = """{"model":"qwen3.5:9b","created_at":"2026-11-16T09:00:00Z","message":{"role":"assistant","content":"Hoi"},"done":true,"done_reason":"stop"}""";
    private const string Tags = """{"models":[{"name":"qwen3.5:9b","size":6600000000},{"name":"qwen3.5:4b","size":3400000000},{"name":"llama-old:1b","size":900000000}]}""";
    private readonly FakeTimeProvider _time = new(RunnerHarness.Start);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AgentSettings Settings(AgentBackendDto backend = AgentBackendDto.Ollama) => AgentSettings.Defaults(backend, RunnerHarness.Start);

    private static StubHttpHandler Ollama(string tags = Tags) => new StubHttpHandler()
        .Json("GET /api/version", """{"version":"0.40.1"}""")
        .Json("GET /api/tags", tags)
        .On("POST /api/show", body => body.Contains("llama-old", StringComparison.Ordinal) ? """{"capabilities":["completion"]}""" : """{"capabilities":["completion","tools"]}""");

    private static async Task<JsonElement> SendAsync(StubHttpHandler stub, IChatClient client, ChatOptions options)
    {
        stub.Json("POST /api/chat", Done);
        await client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")], options, Token).ToListAsync(Token);
        return JsonDocument.Parse(stub.Requests.Last(request => request.Route == "POST /api/chat").Body).RootElement;
    }

    [Fact]
    public async Task Factory_SendsModelContextThinkAndKeepAlive()
    {
        var stub = new StubHttpHandler();
        var factory = new OllamaChatClientFactory(stub.Client());

        var body = await SendAsync(stub, factory.Create("qwen3.5:9b"), OllamaChatClientFactory.Options(think: false, cpuOnly: false));

        body.GetProperty("model").GetString().ShouldBe("qwen3.5:9b");
        body.GetProperty("options").GetProperty("num_ctx").GetInt32().ShouldBe(16384);
        body.GetProperty("options").TryGetProperty("num_gpu", out _).ShouldBeFalse();
        body.GetProperty("think").GetBoolean().ShouldBeFalse();
        body.GetProperty("keep_alive").GetString().ShouldBe("10m");
    }

    [Fact]
    public async Task Factory_CpuFallback_SetsNumGpuZero_AndThinkFollowsTheSetting()
    {
        var stub = new StubHttpHandler();

        var body = await SendAsync(stub, new OllamaChatClientFactory(stub.Client()).Create("qwen3.5:4b"), OllamaChatClientFactory.Options(think: true, cpuOnly: true));

        body.GetProperty("options").GetProperty("num_gpu").GetInt32().ShouldBe(0);
        body.GetProperty("think").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Probe_ReportsVersionModelsAndToolCapability()
    {
        var probe = new OllamaBackendProbe(Ollama().Client(), _time);

        var result = await probe.ProbeAsync("qwen3.5:9b", "qwen3.5:4b", Token);

        (result.Ready, result.Reason, result.Fix).ShouldBe((true, null, null));
        (result.Status.Reachable, result.Status.Version, result.Status.DefaultPresent, result.Status.FallbackPresent).ShouldBe((true, "0.40.1", true, true));
        result.Status.Models.Select(model => (model.Name, model.SizeBytes, model.Tools)).ShouldBe(
        [
            ("qwen3.5:9b", 6_600_000_000L, true), ("qwen3.5:4b", 3_400_000_000L, true), ("llama-old:1b", 900_000_000L, false),
        ]);
    }

    [Fact]
    public async Task Probe_MissingDefaultModel_GivesThePullCommandWithTheSize()
    {
        var probe = new OllamaBackendProbe(Ollama("""{"models":[{"name":"qwen3.5:4b","size":3400000000}]}""").Client(), _time);

        var result = await probe.ProbeAsync("qwen3.5:9b", "qwen3.5:4b", Token);

        result.Ready.ShouldBeFalse();
        result.Reason.ShouldBe("Model qwen3.5:9b is not pulled.");
        result.Fix.ShouldBe("ollama pull qwen3.5:9b (6.6 GB)");
        result.Status.DefaultPresent.ShouldBeFalse();
    }

    [Fact]
    public async Task Probe_MissingUnknownModel_GivesThePullCommandWithoutSize()
    {
        var probe = new OllamaBackendProbe(Ollama().Client(), _time);

        var result = await probe.ProbeAsync("mistral-x:7b", "qwen3.5:4b", Token);

        result.Fix.ShouldBe("ollama pull mistral-x:7b");
    }

    [Fact]
    public async Task Probe_ModelWithoutTools_IsNotReady()
    {
        var probe = new OllamaBackendProbe(Ollama().Client(), _time);

        var result = await probe.ProbeAsync("llama-old:1b", "qwen3.5:4b", Token);

        result.Ready.ShouldBeFalse();
        result.Reason.ShouldBe("Model llama-old:1b does not support tool calling.");
        result.Fix.ShouldBe("Choose a model with tool support in Settings, e.g. qwen3.5:9b.");
    }

    [Fact]
    public async Task Probe_UnreachableOllama_IsNotReadyWithTheFix()
    {
        var stub = new StubHttpHandler { Failure = new HttpRequestException("refused") };

        var result = await new OllamaBackendProbe(stub.Client(), _time).ProbeAsync("qwen3.5:9b", "qwen3.5:4b", Token);

        (result.Ready, result.Status.Reachable).ShouldBe((false, false));
        result.Reason.ShouldBe("Local AI not reachable at http://ollama.test:11434/.");
        result.Fix.ShouldBe("Start Ollama on this computer (https://ollama.com) and keep it running.");
    }

    [Fact]
    public async Task Probe_HangingOllama_GivesUpAfterTwoSeconds()
    {
        var stub = new StubHttpHandler { Hang = true };
        var probe = new OllamaBackendProbe(stub.Client(), _time);

        var pending = probe.ProbeAsync("qwen3.5:9b", "qwen3.5:4b", Token);
        for (var i = 0; i < 200 && !pending.IsCompleted; i++)
        {
            await Task.Delay(5, Token);
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10), Token);
        result.Status.Reachable.ShouldBeFalse();
    }

    [Fact]
    public async Task Probe_BrokenJson_IsNotReachable()
    {
        var stub = new StubHttpHandler().Json("GET /api/version", "not json").Json("GET /api/tags", Tags);

        var result = await new OllamaBackendProbe(stub.Client(), _time).ProbeAsync("qwen3.5:9b", "qwen3.5:4b", Token);

        result.Status.Reachable.ShouldBeFalse();
    }

    [Fact]
    public async Task GpuRelease_UnloadsBothModels()
    {
        var stub = new StubHttpHandler().Json("POST /api/generate", "{}");
        var release = new OllamaGpuRelease(stub.Client(), new InMemorySettingsStore(Settings()), _time);

        await release.ReleaseAsync(Token);

        var bodies = stub.Requests.Where(request => request.Route == "POST /api/generate").Select(request => JsonDocument.Parse(request.Body).RootElement).ToList();
        bodies.Select(body => body.GetProperty("model").GetString()).ShouldBe(["qwen3.5:9b", "qwen3.5:4b"]);
        bodies.ShouldAllBe(body => body.GetProperty("keep_alive").GetInt32() == 0);
    }

    [Fact]
    public async Task GpuRelease_SameModelTwice_IsUnloadedOnce_AndErrorsAreSwallowed()
    {
        var stub = new StubHttpHandler { Failure = new HttpRequestException("down") };
        var settings = Settings() with { OllamaFallbackModel = AgentSettings.DefaultOllamaModel };

        await new OllamaGpuRelease(stub.Client(), new InMemorySettingsStore(settings), _time).ReleaseAsync(Token);

        stub.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GpuRelease_HangingOllama_ReturnsAfterTwoSeconds()
    {
        var stub = new StubHttpHandler { Hang = true };
        var pending = new OllamaGpuRelease(stub.Client(), new InMemorySettingsStore(Settings()), _time).ReleaseAsync(Token);

        for (var i = 0; i < 200 && !pending.IsCompleted; i++)
        {
            await Task.Delay(5, Token);
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        await pending.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }

    [Fact]
    public async Task Selector_UsesTheDefaultModelOnTheGpu_AndTheFallbackOnTheCpuWhileAGpuJobRuns()
    {
        var gpu = Substitute.For<IGpuActivity>();
        var selector = new AgentModelSelector(new InMemorySettingsStore(Settings()), gpu, new ChatBackends { Ollama = new OllamaChatClientFactory(new StubHttpHandler().Client()) });
        var context = new ChatTurnContext(Samples.Run(AgentBackendDto.Ollama), new PageContextDto { Page = "tracks" });

        var idle = await selector.NextTurnAsync(context, Token);
        gpu.IsHeavyJobActive().Returns(true);
        var busy = await selector.NextTurnAsync(context, Token);

        (idle.Model, idle.UsingFallback, idle.ChangeReason).ShouldBe(("qwen3.5:9b", false, null));
        (busy.Model, busy.UsingFallback).ShouldBe(("qwen3.5:4b", true));
        busy.ChangeReason.ShouldBe(AgentModelSelector.FallbackReason);
        busy.Options!.AdditionalProperties.ShouldNotBeNull();
        idle.TimeoutSeconds.ShouldBe(120);
    }

    [Fact]
    public async Task Selector_None_IsUnavailable_AndFakeUsesTheFakeSession()
    {
        var fake = new ScriptedChatSession("""{"version":1,"turns":[{"text":"x"}]}""");
        var selector = new AgentModelSelector(new InMemorySettingsStore(Settings()), Substitute.For<IGpuActivity>(), new ChatBackends { Ollama = new OllamaChatClientFactory(new StubHttpHandler().Client()), Fake = fake });

        var none = await Should.ThrowAsync<BackendUnavailableException>(() => selector.NextTurnAsync(new ChatTurnContext(Samples.Run(AgentBackendDto.None), new PageContextDto { Page = "x" }), Token));
        var claude = await Should.ThrowAsync<BackendUnavailableException>(() => selector.NextTurnAsync(new ChatTurnContext(Samples.Run(AgentBackendDto.Claude), new PageContextDto { Page = "x" }), Token));
        var turn = await selector.NextTurnAsync(new ChatTurnContext(Samples.Run(AgentBackendDto.Fake), new PageContextDto { Page = "x" }), Token);

        none.Message.ShouldBe("The agent is switched off in Settings.");
        claude.Message.ShouldBe("The Claude backend is not configured.");
        turn.Model.ShouldBe(ScriptedChatClient.ModelId);
    }

    [Fact]
    public async Task UnavailableBackend_FailsTheRunWithBackendUnavailable()
    {
        var harness = RunnerHarness.Scripted([TestTools.Echo()]);
        harness.UseSession(new AgentModelSelector(harness.Settings, Substitute.For<IGpuActivity>(), new ChatBackends { Ollama = new OllamaChatClientFactory(new StubHttpHandler().Client()) }));

        var run = await harness.ExecuteAsync();

        (run.Status, run.FailureCode).ShouldBe((AgentRunStatusDto.Failed, AgentFailureCodes.BackendUnavailable));
        run.FailureMessage.ShouldBe("No language model is available for this run.");
    }

    /// <summary>Runs with the given backend.</summary>
    private static class Samples
    {
        public static AgentRun Run(AgentBackendDto backend) => new()
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
    }
}
