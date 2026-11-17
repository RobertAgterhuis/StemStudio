using Microsoft.Extensions.DependencyInjection;
using StemStudio.Agent.Backends;
using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.Tests.LyricAssist;

/// <summary>CTO seam decision (ai-agent / backend-023): ILyricAssistantFactory and AddLyricAssistAgent.</summary>
public sealed class LyricAssistantFactoryTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("lyric-scripts-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static AgentSettings Settings(AgentBackendDto backend, bool notice = true) =>
        AgentSettings.Defaults(backend, RunnerHarness.Start) with { ClaudeNoticeAcknowledgedAt = notice ? RunnerHarness.Start : null };

    private static LyricAssistantFactory Factory(AgentSettings settings, FakeEnvironment? environment = null)
    {
        var env = environment ?? new FakeEnvironment();
        var store = new InMemorySettingsStore(settings);
        var http = new StubHttpHandler();
        var model = new LyricModelClient(store, new StemStudio.Agent.Runtime.IdleGpuActivity(), new LyricModelBackends
        {
            Ollama = new OllamaChatClientFactory(http.Client()),
            Claude = new ClaudeChatClientFactory(env.Keys(), http.Client("https://api.anthropic.com/")),
        });
        return new LyricAssistantFactory(new LyricAssistantServices
        {
            Settings = store,
            ClaudeKeys = env.Keys(),
            Model = model,
            Verification = new FakeVerification(),
            Environment = env,
        });
    }

    [Fact]
    public void Ollama_GetsTheRealModelClient()
    {
        var resolution = Factory(Settings(AgentBackendDto.Ollama)).Resolve(new LyricAssistModelContext("Zomeravond", AssistKindDto.Check));

        resolution.Assistant.ShouldBeOfType<LyricAssistant>();
        resolution.UnavailableReason.ShouldBeNull();
        resolution.Fix.ShouldBeNull();
    }

    [Fact]
    public void Claude_NeedsAUsableKeyAndTheNotice()
    {
        var context = new LyricAssistModelContext("Zomeravond", AssistKindDto.Check);

        Factory(Settings(AgentBackendDto.Claude), FakeEnvironment.WithKey()).Resolve(context).Assistant.ShouldNotBeNull();
        var noKey = Factory(Settings(AgentBackendDto.Claude)).Resolve(context);
        var noNotice = Factory(Settings(AgentBackendDto.Claude, notice: false), FakeEnvironment.WithKey()).Resolve(context);

        noKey.Assistant.ShouldBeNull();
        noKey.UnavailableReason.ShouldBe(LyricAssistProblemTypes.ModelUnavailable);
        noKey.Fix.ShouldNotBeNullOrWhiteSpace();
        noKey.Fix!.ShouldNotContain(FakeEnvironment.SentinelKey);
        noNotice.Assistant.ShouldBeNull();
        noNotice.Fix!.ShouldContain("notice");
    }

    [Theory]
    [InlineData(AgentBackendDto.None)]
    public void NoBackend_IsModelUnavailable(AgentBackendDto backend)
    {
        var resolution = Factory(Settings(backend)).Resolve(new LyricAssistModelContext("x", AssistKindDto.Check));

        resolution.ShouldBe(new LyricAssistantResolution(null, "model-unavailable", LyricAssistantFactory.ChooseBackendFix));
    }

    [Fact]
    public async Task Fake_PlaysTheScriptOfTheSheetAndKind()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "zomeravond-check.json"), """
            {"version":1,"blocks":[{"items":[{"type":"grammar.verb-form","severity":"error","line":10,"original":"word","proposal":"wordt","explanation":"Derde persoon."}]}]}
            """, TestContext.Current.CancellationToken);
        var environment = new FakeEnvironment((LyricAssistantFactory.FakeScriptVariable, _folder));
        var resolution = Factory(Settings(AgentBackendDto.Fake), environment).Resolve(new LyricAssistModelContext("Zomeravond", AssistKindDto.Check));
        var sink = new RecordingLyricSink();

        await resolution.Assistant!.RunAsync(LyricFixtures.Job() with { Blocks = [LyricFixtures.Block1 with { Index = 0 }] }, sink, TestContext.Current.CancellationToken);

        sink.Model!.Backend.ShouldBe(AgentBackendDto.Fake);
        sink.Blocks.Single().Suggestions.Single().Proposal.ShouldBe("wordt");
    }

    [Fact]
    public async Task Fake_WithoutAMatchingScript_AnswersEmptyBlocks()
    {
        var environment = new FakeEnvironment((LyricAssistantFactory.FakeScriptVariable, _folder));
        var resolution = Factory(Settings(AgentBackendDto.Fake), environment).Resolve(new LyricAssistModelContext("Unknown", AssistKindDto.Titles));
        var sink = new RecordingLyricSink();

        await resolution.Assistant!.RunAsync(LyricFixtures.Job(AssistKindDto.Titles) with { Blocks = [LyricFixtures.Block0] }, sink, TestContext.Current.CancellationToken);

        sink.Blocks.Single().Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Fake_WithoutTheVariableOrWithABrokenScript_IsModelUnavailable()
    {
        var broken = Path.Combine(_folder, "broken.json");
        await File.WriteAllTextAsync(broken, "{", TestContext.Current.CancellationToken);
        var context = new LyricAssistModelContext("x", AssistKindDto.Check);

        Factory(Settings(AgentBackendDto.Fake)).Resolve(context).Fix.ShouldBe(LyricAssistantFactory.FakeScriptFix);
        Factory(Settings(AgentBackendDto.Fake), new FakeEnvironment((LyricAssistantFactory.FakeScriptVariable, broken))).Resolve(context).UnavailableReason.ShouldBe("model-unavailable");
        Should.Throw<ArgumentNullException>(() => Factory(Settings(AgentBackendDto.Ollama)).Resolve(null!));
    }

    [Fact]
    public void AddLyricAssistAgent_RegistersTheFactory_WithTheHostsVerificationAndSettings()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentSettingsStore>(new InMemorySettingsStore(Settings(AgentBackendDto.Ollama)));
        services.AddSingleton<ILyricVerification>(new FakeVerification());
        services.AddSingleton<IEnvironmentReader>(new FakeEnvironment(
            (LyricAssistServiceCollectionExtensions.MaxInputTokensVariable, "5000"), (LyricAssistServiceCollectionExtensions.BlockTimeoutVariable, "20")));

        using var provider = services.AddLyricAssistAgent().AddLyricAssistAgent().BuildServiceProvider();

        provider.GetRequiredService<ILyricAssistantFactory>().Resolve(new LyricAssistModelContext("x", AssistKindDto.Check)).Assistant.ShouldNotBeNull();
        var options = provider.GetRequiredService<LyricAssistOptions>();
        options.MaxInputTokensPerAssist.ShouldBe(5000);
        options.MaxOutputTokensPerAssist.ShouldBe(LyricAssistLimits.DefaultMaxOutputTokensPerAssist);
        options.LocalBlockTimeout.ShouldBe(TimeSpan.FromSeconds(20));
        options.ClaudeBlockTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        Should.Throw<ArgumentNullException>(() => LyricAssistServiceCollectionExtensions.AddLyricAssistAgent(null!));
    }

    [Fact]
    public void Options_IgnoreInvalidValues()
    {
        var options = LyricAssistServiceCollectionExtensions.Options(new FakeEnvironment((LyricAssistServiceCollectionExtensions.MaxOutputTokensVariable, "-3")));

        options.ShouldBe(new LyricAssistOptions());
        LyricAssistantResolution.Of(new LyricAssistant(new StemStudio.Agent.LyricAssist.Fakes.ScriptedLyricModel(StemStudio.Agent.LyricAssist.Fakes.LyricScript.Parse("""{"version":1,"blocks":[]}""")), new FakeVerification())).Assistant.ShouldNotBeNull();
    }
}

/// <summary>Records what the assistant reports.</summary>
public sealed class RecordingLyricSink : ILyricAssistSink
{
    public LyricModelInfo? Model { get; private set; }

    public List<LyricBlockResult> Blocks { get; } = [];

    public List<(AssistNoteCodeDto Code, string Message)> Notes { get; } = [];

    public List<TokenUsageDto> Usage { get; } = [];

    public List<string> Order { get; } = [];

    public Task StartedAsync(LyricModelInfo model, CancellationToken cancellationToken)
    {
        Model = model;
        Order.Add("started");
        return Task.CompletedTask;
    }

    public Task BlockCompletedAsync(LyricBlockResult result, CancellationToken cancellationToken)
    {
        Blocks.Add(result);
        Order.Add($"block {result.BlockIndex}");
        return Task.CompletedTask;
    }

    public Task NoteAsync(AssistNoteCodeDto code, string message, CancellationToken cancellationToken)
    {
        Notes.Add((code, message));
        Order.Add($"note {code}");
        return Task.CompletedTask;
    }

    public Task UsageAsync(TokenUsageDto usage, CancellationToken cancellationToken)
    {
        Usage.Add(usage);
        Order.Add("usage");
        return Task.CompletedTask;
    }
}
