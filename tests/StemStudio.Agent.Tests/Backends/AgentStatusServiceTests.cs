using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Generation;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Agent.Tests.Backends;

/// <summary>US3/US4: <see cref="AgentStatusService"/> - status per backend without generating text, explicit tests, tool list.</summary>
public sealed class AgentStatusServiceTests : IDisposable
{
    private const string Tags = """{"models":[{"name":"qwen3.5:9b","size":6600000000},{"name":"qwen3.5:4b","size":3400000000}]}""";
    private readonly FakeTimeProvider _time = new(RunnerHarness.Start);
    private readonly IGpuActivity _gpu = Substitute.For<IGpuActivity>();
    private readonly StubHttpHandler _claude = new() { Failure = new InvalidOperationException("Claude must not be called.") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _claude.Dispose();

    private static StubHttpHandler Ollama(string tags = Tags) => new StubHttpHandler()
        .Json("GET /api/version", """{"version":"0.40.1"}""")
        .Json("GET /api/tags", tags)
        .Json("POST /api/show", """{"capabilities":["completion","tools"]}""");

    private AgentStatusService Service(AgentSettings settings, StubHttpHandler ollama, IEnvironmentReader? environment = null, bool fake = false)
    {
        var generations = Substitute.For<IGenerationActions>();
        var reader = new ServiceStatusReader(generations, _time);
        var probes = new BackendProbes
        {
            Ollama = new OllamaBackendProbe(ollama.Client(), _time),
            Claude = new ClaudeBackendProbe(new ClaudeChatClientFactory(new ClaudeKeyProvider(environment ?? new FakeEnvironment(), new FakeStoredClaudeKeyReader()), _claude.Client("https://api.anthropic.com/")), _time),
            FakeAvailable = fake,
        };
        return new AgentStatusService(new InMemorySettingsStore(settings), _gpu, probes, new ToolCatalog(new AgentToolRegistry([new Core001ToolProvider(), new MetaToolProvider()]), reader));
    }

    private static AgentSettings Settings(AgentBackendDto backend) => AgentSettings.Defaults(backend, RunnerHarness.Start);

    [Fact]
    public async Task Ollama_Ready_ReportsTheDefaultModel_AndNeverCallsClaude()
    {
        var status = await Service(Settings(AgentBackendDto.Ollama), Ollama()).GetStatusAsync(Token);

        (status.Backend, status.Ready, status.ActiveModel, status.UsingFallback, status.Reason).ShouldBe((AgentBackendDto.Ollama, true, "qwen3.5:9b", false, null));
        status.Ollama.Version.ShouldBe("0.40.1");
        status.Claude.KeyConfigured.ShouldBeFalse();
        _claude.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Ollama_GpuBusy_UsesTheFallback()
    {
        _gpu.IsHeavyJobActive().Returns(true);

        var status = await Service(Settings(AgentBackendDto.Ollama), Ollama()).GetStatusAsync(Token);

        (status.Ready, status.ActiveModel, status.UsingFallback).ShouldBe((true, "qwen3.5:4b", true));
    }

    [Fact]
    public async Task Ollama_GpuBusy_WithoutTheFallbackModel_IsNotReady()
    {
        _gpu.IsHeavyJobActive().Returns(true);

        var status = await Service(Settings(AgentBackendDto.Ollama), Ollama("""{"models":[{"name":"qwen3.5:9b","size":1}]}""")).GetStatusAsync(Token);

        status.Ready.ShouldBeFalse();
        (status.Reason, status.Fix).ShouldBe(("Model qwen3.5:4b is not pulled.", "ollama pull qwen3.5:4b (3.4 GB)"));
    }

    [Fact]
    public async Task Ollama_Down_GivesReasonAndFix()
    {
        var status = await Service(Settings(AgentBackendDto.Ollama), new StubHttpHandler { Failure = new HttpRequestException("refused") }).GetStatusAsync(Token);

        status.Ready.ShouldBeFalse();
        status.Reason.ShouldBe("Local AI not reachable at http://ollama.test:11434/.");
        status.Fix.ShouldNotBeNull();
    }

    [Fact]
    public async Task None_IsNotReady_AndProbesNothingRemote()
    {
        var ollama = Ollama();

        var status = await Service(Settings(AgentBackendDto.None), ollama).GetStatusAsync(Token);

        (status.Ready, status.ActiveModel, status.Reason, status.Fix).ShouldBe((false, null, AgentStatusService.OffReason, AgentStatusService.OffFix));
        _claude.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Claude_ReadyOnlyWithKeyAndNotice_AndStatusNeverCallsClaude()
    {
        var acknowledged = Settings(AgentBackendDto.Claude) with { ClaudeNoticeAcknowledgedAt = RunnerHarness.Start };

        var ready = await Service(acknowledged, Ollama(), FakeEnvironment.WithKey()).GetStatusAsync(Token);
        var noKey = await Service(acknowledged, Ollama()).GetStatusAsync(Token);
        var noNotice = await Service(Settings(AgentBackendDto.Claude), Ollama(), FakeEnvironment.WithKey()).GetStatusAsync(Token);

        (ready.Ready, ready.ActiveModel, ready.Claude.KeyConfigured).ShouldBe((true, "claude-opus-5-5", true));
        (noKey.Ready, noKey.Reason).ShouldBe((false, ClaudeErrors.KeyMissing));
        (noNotice.Ready, noNotice.Reason).ShouldBe((false, ClaudeErrors.NoticeMissing));
        _claude.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Fake_IsReadyOnlyWithAScript()
    {
        var withScript = await Service(Settings(AgentBackendDto.Fake), Ollama(), fake: true).GetStatusAsync(Token);
        var without = await Service(Settings(AgentBackendDto.Fake), Ollama()).GetStatusAsync(Token);

        (withScript.Ready, withScript.ActiveModel).ShouldBe((true, "scripted"));
        (without.Ready, without.Reason).ShouldBe((false, AgentStatusService.FakeMissingReason));
    }

    [Fact]
    public async Task Test_Ollama_ProbesAndReportsLatency()
    {
        var result = await Service(Settings(AgentBackendDto.Ollama), Ollama()).TestAsync(AgentBackendDto.Ollama, Token);

        (result.Backend, result.Ok, result.LatencyMs).ShouldBe((AgentBackendDto.Ollama, true, 0));
        result.Detail.ShouldBe("Ollama 0.40.1 answered; model qwen3.5:9b supports tool calling.");
    }

    [Fact]
    public async Task Test_OllamaDown_IsNotOk()
    {
        var result = await Service(Settings(AgentBackendDto.Ollama), new StubHttpHandler { Failure = new HttpRequestException("x") }).TestAsync(AgentBackendDto.Ollama, Token);

        result.Ok.ShouldBeFalse();
        result.Detail.ShouldStartWith("Local AI not reachable");
    }

    [Fact]
    public async Task Test_OtherBackends_AreNotTestable()
    {
        var result = await Service(Settings(AgentBackendDto.Ollama), Ollama()).TestAsync(AgentBackendDto.None, Token);

        (result.Ok, result.Detail).ShouldBe((false, AgentStatusService.NotTestable));
    }

    [Fact]
    public async Task Test_Claude_WithoutKey_SendsNothing()
    {
        var result = await Service(Settings(AgentBackendDto.Ollama), Ollama()).TestAsync(AgentBackendDto.Claude, Token);

        (result.Backend, result.Ok).ShouldBe((AgentBackendDto.Claude, false));
        _claude.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void ListTools_ShowsThePageProfileWithAvailability()
    {
        var tools = Service(Settings(AgentBackendDto.Ollama), Ollama()).ListTools(new PageContextDto { Page = "tracks" });

        tools.Select(tool => tool.Name).ShouldBe(tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        tools.ShouldContain(tool => tool.Name == "search_library" && tool.Available);
        tools.ShouldContain(tool => tool.Name == "set_plan");
    }

    [Fact]
    public async Task ListTools_UsesTheLastKnownServiceState()
    {
        var generations = Substitute.For<IGenerationActions>();
        generations.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(new GenerationStatusDto { State = GenerationAvailabilityDto.Ready, Voice = new VoiceServiceStatusDto { State = GenerationAvailabilityDto.Ready } });
        var reader = new ServiceStatusReader(generations, _time);
        var catalog = new ToolCatalog(new AgentToolRegistry([new Core001ToolProvider()]), reader);

        var before = catalog.Peek(new PageContextDto { Page = "tracks" });
        await reader.ContextAsync(new PageContextDto { Page = "tracks" }, Token);
        var after = catalog.Peek(new PageContextDto { Page = "tracks" });

        before.Generation.ShouldBe(ServiceAvailability.Unknown);
        after.Generation.ShouldBe(ServiceAvailability.Available);
    }
}
