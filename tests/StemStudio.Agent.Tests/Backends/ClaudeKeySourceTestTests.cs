using System.Net;
using System.Text.RegularExpressions;
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
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Agent.Tests.Backends;

/// <summary>
/// 011 follow-up claude-key T036 (US4, CK-FR-007, CK-SC-002): the explicit Claude test with <c>claudeKeySource</c>
/// <c>active</c>/<c>stored</c>/<c>environment</c> sends exactly one model lookup with the key of the requested source to a stub
/// handler, reports <c>keySource</c>, and answers a source without a usable key with <c>ok: false</c> and a readable detail
/// without any request. Anthropic is never called.
/// </summary>
public sealed class ClaudeKeySourceTestTests
{
    private const string Lookup = "GET /v1/models/claude-opus-5-5";
    private const string ModelJson = """{"type":"model","id":"claude-opus-5-5","display_name":"Claude Opus 5.5","created_at":"2026-10-01T00:00:00Z"}""";
    private readonly FakeTimeProvider _time = new(RunnerHarness.Start);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AgentSettings Settings() => AgentSettings.Defaults(AgentBackendDto.Claude, RunnerHarness.Start);

    private static FakeStoredClaudeKeyReader StoredReader() => new()
    {
        Current = new StoredClaudeKey
        {
            State = StoredClaudeKeyStateDto.Readable,
            Key = ClaudeApiKey.Parse(ClaudeKeySentinels.Stored).Key,
            LastFour = "STO1",
            StoreAvailable = true,
        },
    };

    private static ClaudeKeyProvider Both() => new(FakeEnvironment.WithKey(), StoredReader());

    private ClaudeBackendProbe Probe(StubHttpHandler stub, IClaudeKeyProvider keys) =>
        new(new ClaudeChatClientFactory(keys, stub.Client("https://api.anthropic.com/")), _time);

    private static StubHttpHandler Answering() => new StubHttpHandler().Json(Lookup, ModelJson);

    private static StubHttpHandler MustNotBeCalled() => new() { Failure = new InvalidOperationException("Claude must not be called.") };

    private static string SentKey(StubHttpHandler stub) =>
        Regex.Matches(stub.Requests.Single().Headers, Regex.Escape(ClaudeKeySentinels.Prefix) + "[A-Za-z0-9_-]+").Select(match => match.Value).Distinct().Single();

    [Theory]
    [InlineData(ClaudeKeyTestSourceDto.Active, ClaudeKeySourceDto.Environment)]
    [InlineData(ClaudeKeyTestSourceDto.Environment, ClaudeKeySourceDto.Environment)]
    [InlineData(ClaudeKeyTestSourceDto.Stored, ClaudeKeySourceDto.Stored)]
    public async Task Test_UsesTheKeyOfTheRequestedSource(ClaudeKeyTestSourceDto requested, ClaudeKeySourceDto used)
    {
        var stub = Answering();

        var result = await Probe(stub, Both()).TestAsync(Settings(), requested, Token);

        (result.Backend, result.Ok, result.KeySource).ShouldBe((AgentBackendDto.Claude, true, (ClaudeKeySourceDto?)used));
        SentKey(stub).ShouldBe(used == ClaudeKeySourceDto.Stored ? ClaudeKeySentinels.Stored : ClaudeKeySentinels.Environment);
        stub.Requests.Select(request => request.Route).ShouldBe([Lookup]);
        result.Detail.ShouldBe("Claude answered; model claude-opus-5-5 is available.");
    }

    [Fact]
    public async Task Test_ActiveFollowsThePrecedenceToTheStoredKey()
    {
        var stub = Answering();

        var result = await Probe(stub, new ClaudeKeyProvider(new FakeEnvironment(), StoredReader())).TestAsync(Settings(), ClaudeKeyTestSourceDto.Active, Token);

        (result.Ok, result.KeySource).ShouldBe((true, (ClaudeKeySourceDto?)ClaudeKeySourceDto.Stored));
        SentKey(stub).ShouldBe(ClaudeKeySentinels.Stored);
    }

    [Fact]
    public async Task Test_WithoutSource_IsTheActiveKey()
    {
        var stub = Answering();

        var result = await Probe(stub, Both()).TestAsync(Settings(), Token);

        result.KeySource.ShouldBe(ClaudeKeySourceDto.Environment);
        SentKey(stub).ShouldBe(ClaudeKeySentinels.Environment);
    }

    [Fact]
    public async Task Test_StoredRequestedButNoneStored_IsNotOkWithoutARequest()
    {
        var stub = MustNotBeCalled();

        var result = await Probe(stub, FakeEnvironment.WithKey().Keys()).TestAsync(Settings(), ClaudeKeyTestSourceDto.Stored, Token);

        (result.Ok, result.KeySource, result.Detail).ShouldBe((false, (ClaudeKeySourceDto?)null, ClaudeBackendProbe.NoStoredKey));
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Test_EnvironmentRequestedButNotSet_IsNotOkWithoutARequest()
    {
        var stub = MustNotBeCalled();

        var result = await Probe(stub, new ClaudeKeyProvider(new FakeEnvironment(), StoredReader())).TestAsync(Settings(), ClaudeKeyTestSourceDto.Environment, Token);

        (result.Ok, result.KeySource, result.Detail).ShouldBe((false, (ClaudeKeySourceDto?)null, ClaudeBackendProbe.NoEnvironmentKey));
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Test_ActiveWithAnUnreadableStoredKey_NamesTheReason()
    {
        var stub = MustNotBeCalled();
        var keys = new ClaudeKeyProvider(new FakeEnvironment(), FakeStoredClaudeKeyReader.Unreadable("STO1", RunnerHarness.Start));

        var result = await Probe(stub, keys).TestAsync(Settings(), ClaudeKeyTestSourceDto.Active, Token);

        (result.Ok, result.KeySource).ShouldBe((false, (ClaudeKeySourceDto?)null));
        result.Detail.ShouldBe(ClaudeErrors.KeyUnusable(ClaudeKeyProvider.UnreadableReason, ClaudeKeyProvider.UnreadableFix));
        stub.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Claude rejected the API key (HTTP 401)")]
    [InlineData(HttpStatusCode.Forbidden, "Claude rejected the API key (HTTP 403)")]
    public async Task Test_RejectedKey_IsNotOkAndKeepsTheSource(HttpStatusCode status, string text)
    {
        var stub = new StubHttpHandler().Json(Lookup, $$$"""{"type":"error","error":{"type":"authentication_error","message":"{{{ClaudeKeySentinels.Stored}}}"}}""", status);

        var result = await Probe(stub, Both()).TestAsync(Settings(), ClaudeKeyTestSourceDto.Stored, Token);

        (result.Ok, result.KeySource).ShouldBe((false, (ClaudeKeySourceDto?)ClaudeKeySourceDto.Stored));
        result.Detail.ShouldStartWith(text);
        stub.Requests.Count.ShouldBe(1);
        foreach (var form in ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored))
        {
            result.Detail.ShouldNotContain(form);
        }
    }

    [Fact]
    public async Task Test_NetworkFailure_IsUnreachable()
    {
        var stub = new StubHttpHandler { Failure = new HttpRequestException("no route") };

        var result = await Probe(stub, Both()).TestAsync(Settings(), ClaudeKeyTestSourceDto.Stored, Token);

        (result.Ok, result.KeySource, result.Detail).ShouldBe((false, (ClaudeKeySourceDto?)ClaudeKeySourceDto.Stored, ClaudeErrors.Unreachable));
        stub.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Test_Timeout_IsUnreachable()
    {
        var stub = new StubHttpHandler { Hang = true };
        var probe = Probe(stub, Both());

        var pending = probe.TestAsync(Settings(), ClaudeKeyTestSourceDto.Environment, Token);
        await WaitForRequestAsync(stub);
        _time.Advance(ClaudeBackendProbe.Timeout);
        var result = await pending;

        (result.Ok, result.KeySource, result.Detail).ShouldBe((false, (ClaudeKeySourceDto?)ClaudeKeySourceDto.Environment, ClaudeErrors.Unreachable));
        stub.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StatusService_PassesTheSourceToTheClaudeTest()
    {
        var stub = Answering();
        var service = StatusService(stub, Both());

        var stored = await service.TestAsync(AgentBackendDto.Claude, ClaudeKeyTestSourceDto.Stored, Token);
        var active = await service.TestAsync(AgentBackendDto.Claude, Token);

        (stored.KeySource, active.KeySource).ShouldBe(((ClaudeKeySourceDto?)ClaudeKeySourceDto.Stored, (ClaudeKeySourceDto?)ClaudeKeySourceDto.Environment));
        stub.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task StatusService_IgnoresTheSourceForOtherBackends()
    {
        var result = await StatusService(MustNotBeCalled(), Both()).TestAsync(AgentBackendDto.Fake, ClaudeKeyTestSourceDto.Stored, Token);

        (result.Ok, result.KeySource, result.Detail).ShouldBe((false, (ClaudeKeySourceDto?)null, AgentStatusService.NotTestable));
    }

    private AgentStatusService StatusService(StubHttpHandler claude, IClaudeKeyProvider keys)
    {
        var probes = new BackendProbes
        {
            Ollama = new OllamaBackendProbe(new StubHttpHandler { Failure = new HttpRequestException("down") }.Client(), _time),
            Claude = Probe(claude, keys),
        };
        var catalog = new ToolCatalog(new AgentToolRegistry([new MetaToolProvider()]), new ServiceStatusReader(Substitute.For<IGenerationActions>(), _time));
        return new AgentStatusService(new InMemorySettingsStore(Settings()), Substitute.For<IGpuActivity>(), probes, catalog);
    }

    private static async Task WaitForRequestAsync(StubHttpHandler stub)
    {
        for (var i = 0; i < 500 && stub.Requests.Count == 0; i++)
        {
            await Task.Delay(5, Token);
        }
    }
}
