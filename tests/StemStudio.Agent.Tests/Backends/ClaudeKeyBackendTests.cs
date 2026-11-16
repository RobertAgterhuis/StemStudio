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
/// 011 follow-up claude-key T021 (US1, CK-FR-001, CK-FR-008, CK-FR-016): <see cref="ClaudeChatClientFactory"/>,
/// <see cref="ClaudeBackendProbe"/> and <see cref="AgentStatusService"/> read the key only through
/// <see cref="IClaudeKeyProvider"/>; the client cache is keyed by fingerprint; <c>ClaudeStatusDto.Key</c> is filled; a key
/// change applies to the next run while an active run keeps its key; the SC-006 sentinel proof covers stored keys. Every
/// request goes to a stub handler; Anthropic is never called.
/// </summary>
public sealed class ClaudeKeyBackendTests
{
    private const string Messages = "POST /v1/messages";
    private static readonly DateTimeOffset StoredAt = new(2026, 11, 16, 15, 40, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _time = new(RunnerHarness.Start);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AgentSettings Allowed() =>
        AgentSettings.Defaults(AgentBackendDto.Claude, RunnerHarness.Start) with { ClaudeNoticeAcknowledgedAt = RunnerHarness.Start };

    private static ClaudeApiKey Key(string value) => ClaudeApiKey.Parse(value).Key!;

    private static FakeStoredClaudeKeyReader StoredReader(string value = "") => new()
    {
        Current = new StoredClaudeKey
        {
            State = StoredClaudeKeyStateDto.Readable,
            Key = Key(value.Length == 0 ? ClaudeKeySentinels.Stored : value),
            LastFour = "STO1",
            StoredAt = StoredAt,
            StoreAvailable = true,
        },
    };

    private static ClaudeKeyProvider StoredOnly(FakeStoredClaudeKeyReader? reader = null) => new(new FakeEnvironment(), reader ?? StoredReader());

    private static ClaudeChatClientFactory Factory(StubHttpHandler stub, IClaudeKeyProvider keys) => new(keys, stub.Client("https://api.anthropic.com/"));

    private static StubHttpHandler MustNotBeCalled() => new() { Failure = new InvalidOperationException("Claude must not be called.") };

    private static StubHttpHandler Answering() => new StubHttpHandler().Sse(Messages, ClaudeSse.Text("ok"));

    // The one key-shaped value in the request headers (the SDK sends the key as a header).
    private static string ApiKeyHeader(string headers) =>
        Regex.Matches(headers, Regex.Escape(ClaudeKeySentinels.Prefix) + "[A-Za-z0-9_-]+").Select(match => match.Value).Distinct().Single();

    [Fact]
    public async Task Factory_UsesTheStoredKeyFromTheProvider()
    {
        var stub = Answering();
        var session = new ClaudeChatSession(new InMemorySettingsStore(Allowed()), Factory(stub, StoredOnly()));

        await ClaudeSse.SendAsync(await session.NextTurnAsync(Context(NewRun()), Token), Token);

        ApiKeyHeader(stub.Requests.Single().Headers).ShouldBe(ClaudeKeySentinels.Stored);
    }

    [Fact]
    public void Factory_ReadsTheKeyOnlyThroughTheProvider()
    {
        var keys = new FakeClaudeKeyProvider { Resolution = Resolution(Key(ClaudeKeySentinels.Stored), ClaudeKeySourceDto.Stored) };

        var key = Factory(MustNotBeCalled(), keys).ActiveKey();

        key.Reveal().ShouldBe(ClaudeKeySentinels.Stored);
        keys.ResolveCount.ShouldBe(1);
    }

    [Fact]
    public void Factory_CachesOneClientPerKeyFingerprint()
    {
        var factory = Factory(MustNotBeCalled(), new FakeClaudeKeyProvider());

        var first = Sdk(factory.Create("claude-opus-5-5", Key(ClaudeKeySentinels.Stored)));
        var sameKeyOtherInstance = Sdk(factory.Create("claude-haiku-5-5", Key(ClaudeKeySentinels.Stored)));
        var other = Sdk(factory.Create("claude-opus-5-5", Key(ClaudeKeySentinels.Replacement)));

        first.ShouldNotBeNull();
        sameKeyOtherInstance.ShouldBeSameAs(first);
        other.ShouldNotBeSameAs(first);
    }

    [Fact]
    public void Factory_WithoutAUsableKey_FailsWithTheReasonOfTheState()
    {
        var unreadable = StoredOnly(FakeStoredClaudeKeyReader.Unreadable("STO1", StoredAt));
        var nothing = new ClaudeKeyProvider(new FakeEnvironment(), new FakeStoredClaudeKeyReader());

        var stored = Should.Throw<BackendUnavailableException>(() => Factory(MustNotBeCalled(), unreadable).Create("claude-opus-5-5"));
        var missing = Should.Throw<BackendUnavailableException>(() => Factory(MustNotBeCalled(), nothing).Create("claude-opus-5-5"));

        stored.Message.ShouldBe($"{ClaudeKeyProvider.UnreadableReason} {ClaudeKeyProvider.UnreadableFix} {ClaudeErrors.SwitchToLocal}");
        missing.Message.ShouldBe(ClaudeErrors.KeyMissing);
        missing.Message.ShouldContain("Settings");
    }

    [Fact]
    public async Task Session_UnreadableStoredKey_FailsWithTheReasonAndSendsNothing()
    {
        var stub = MustNotBeCalled();
        var session = new ClaudeChatSession(new InMemorySettingsStore(Allowed()), Factory(stub, StoredOnly(FakeStoredClaudeKeyReader.Unreadable("STO1", StoredAt))));

        var failure = await Should.ThrowAsync<BackendUnavailableException>(() => session.NextTurnAsync(Context(NewRun()), Token));

        failure.Message.ShouldStartWith(ClaudeKeyProvider.UnreadableReason);
        failure.Message.ShouldEndWith(ClaudeErrors.SwitchToLocal);
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task KeyChange_AppliesToTheNextRun_WhileTheActiveRunKeepsItsKey()
    {
        var stub = Answering();
        var reader = StoredReader();
        var keys = StoredOnly(reader);
        var session = new ClaudeChatSession(new InMemorySettingsStore(Allowed()), Factory(stub, keys));
        var active = NewRun();

        await ClaudeSse.SendAsync(await session.NextTurnAsync(Context(active), Token), Token);
        reader.Current = reader.Current with { Key = Key(ClaudeKeySentinels.Replacement), LastFour = "STO2" };
        keys.Invalidate();
        await ClaudeSse.SendAsync(await session.NextTurnAsync(Context(active with { ModelTurns = 1 }), Token), Token);
        await ClaudeSse.SendAsync(await session.NextTurnAsync(Context(NewRun()), Token), Token);

        stub.Requests.Select(request => ApiKeyHeader(request.Headers))
            .ShouldBe([ClaudeKeySentinels.Stored, ClaudeKeySentinels.Stored, ClaudeKeySentinels.Replacement]);
    }

    [Fact]
    public async Task KeyChange_TheFirstTurnOfARunAlwaysTakesTheCurrentKey()
    {
        var stub = Answering();
        var reader = StoredReader();
        var keys = StoredOnly(reader);
        var session = new ClaudeChatSession(new InMemorySettingsStore(Allowed()), Factory(stub, keys));
        var run = NewRun();

        await ClaudeSse.SendAsync(await session.NextTurnAsync(Context(run), Token), Token);
        reader.Current = reader.Current with { Key = Key(ClaudeKeySentinels.Replacement) };
        keys.Invalidate();
        await ClaudeSse.SendAsync(await session.NextTurnAsync(Context(run), Token), Token);

        ApiKeyHeader(stub.Requests[^1].Headers).ShouldBe(ClaudeKeySentinels.Replacement);
    }

    [Fact]
    public async Task KeyPins_AreBoundedAndARunWithoutAPinTakesTheCurrentKey()
    {
        var stub = new StubHttpHandler().Sse(Messages, ClaudeSse.Text("ok"));
        var reader = StoredReader();
        var keys = StoredOnly(reader);
        var session = new ClaudeChatSession(new InMemorySettingsStore(Allowed()), Factory(stub, keys));
        var first = NewRun();
        await session.NextTurnAsync(Context(first), Token);
        for (var i = 0; i < ClaudeChatSession.PinnedRuns; i++)
        {
            await session.NextTurnAsync(Context(NewRun()), Token);
        }

        reader.Current = reader.Current with { Key = Key(ClaudeKeySentinels.Replacement) };
        keys.Invalidate();
        await ClaudeSse.SendAsync(await session.NextTurnAsync(Context(first with { ModelTurns = 3 }), Token), Token);

        ApiKeyHeader(stub.Requests.Single().Headers).ShouldBe(ClaudeKeySentinels.Replacement);
    }

    [Fact]
    public void Probe_FillsTheKeyStatusOfAReadableStoredKey_WithoutAnyRequest()
    {
        var stub = MustNotBeCalled();

        var status = new ClaudeBackendProbe(Factory(stub, StoredOnly()), _time).Status(Allowed());

        status.KeyConfigured.ShouldBeTrue();
        status.Key.ShouldBe(new ClaudeKeyStatusDto
        {
            Configured = true,
            Source = ClaudeKeySourceDto.Stored,
            State = ClaudeKeyStateDto.Stored,
            LastFour = "STO1",
            Stored = StoredClaudeKeyStateDto.Readable,
            StoredLastFour = "STO1",
            StoredAt = StoredAt,
            StoreAvailable = true,
            Reason = null,
            Fix = null,
        });
        stub.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Probe_ReportsTheEnvironmentKeyOverTheStoredKey()
    {
        var keys = new ClaudeKeyProvider(FakeEnvironment.WithKey(), StoredReader());

        var status = new ClaudeBackendProbe(Factory(MustNotBeCalled(), keys), _time).Status(Allowed());

        (status.KeyConfigured, status.Key.State, status.Key.Source, status.Key.LastFour, status.Key.Stored, status.Key.StoredLastFour)
            .ShouldBe((true, ClaudeKeyStateDto.Environment, (ClaudeKeySourceDto?)ClaudeKeySourceDto.Environment, "ENV1", StoredClaudeKeyStateDto.Readable, "STO1"));
    }

    [Fact]
    public void KeyStatus_MapsEveryFieldOfTheResolution()
    {
        var resolution = new ClaudeKeyResolution
        {
            State = ClaudeKeyStateDto.StoredUnreadable,
            Stored = StoredClaudeKeyStateDto.Unreadable,
            StoredLastFour = "wXyZ",
            StoredAt = StoredAt,
            StoreAvailable = false,
            StoreUnavailableReason = KeyStoreUnavailableReasons.InsideLibrary,
            Reason = "r",
            Fix = "f",
        };

        ClaudeKeyStatus.ToDto(resolution).ShouldBe(new ClaudeKeyStatusDto
        {
            Configured = false,
            Source = null,
            State = ClaudeKeyStateDto.StoredUnreadable,
            LastFour = null,
            Stored = StoredClaudeKeyStateDto.Unreadable,
            StoredLastFour = "wXyZ",
            StoredAt = StoredAt,
            StoreAvailable = false,
            Reason = "r",
            Fix = "f",
        });
        Should.Throw<ArgumentNullException>(() => ClaudeKeyStatus.ToDto(null!));
    }

    [Fact]
    public async Task Status_ReadableStoredKey_IsReadyAndKeyConfigured()
    {
        var status = await StatusService(StoredOnly()).GetStatusAsync(Token);

        (status.Ready, status.ActiveModel, status.Claude.KeyConfigured, status.Claude.Key.State)
            .ShouldBe((true, "claude-opus-5-5", true, ClaudeKeyStateDto.Stored));
    }

    [Fact]
    public async Task Status_UnreadableStoredKey_IsNotReadyWithTheReasonAndFix()
    {
        var status = await StatusService(StoredOnly(FakeStoredClaudeKeyReader.Unreadable("STO1", StoredAt))).GetStatusAsync(Token);

        (status.Ready, status.Claude.KeyConfigured, status.Claude.Key.State).ShouldBe((false, false, ClaudeKeyStateDto.StoredUnreadable));
        status.Reason.ShouldBe($"{ClaudeKeyProvider.UnreadableReason} {ClaudeErrors.SwitchToLocal}");
        status.Fix.ShouldBe(ClaudeKeyProvider.UnreadableFix);
    }

    [Fact]
    public async Task Status_NoKey_PointsToSettingsAndTheEnvironmentVariable()
    {
        var status = await StatusService(new ClaudeKeyProvider(new FakeEnvironment(), new FakeStoredClaudeKeyReader())).GetStatusAsync(Token);

        (status.Ready, status.Reason, status.Fix).ShouldBe((false, ClaudeErrors.KeyMissing, ClaudeErrors.KeyMissingFix));
    }

    [Fact]
    public async Task Sentinel_StoredAndEnvironmentKeysNeverAppearInEventsLogsOrFailures()
    {
        foreach (var (keys, sentinel) in new (IClaudeKeyProvider, string)[]
        {
            (StoredOnly(), ClaudeKeySentinels.Stored),
            (new ClaudeKeyProvider(FakeEnvironment.WithKey(), StoredReader()), ClaudeKeySentinels.Environment),
        })
        {
            var stub = new StubHttpHandler().Json(Messages, $$$"""{"type":"error","error":{"type":"x","message":"echo {{{sentinel}}}"}}""", HttpStatusCode.Unauthorized);
            var logger = new CapturingLogger();
            var harness = new RunnerHarness(new ThrowingChatClient(new InvalidOperationException("unused")), [TestTools.Echo()]) { Logger = logger };
            await harness.Settings.SaveAsync(Allowed(), Token);
            harness.UseBackend(AgentBackendDto.Claude);
            harness.UseSession(new ClaudeChatSession(harness.Settings, Factory(stub, keys)));

            var run = await harness.ExecuteAsync();
            var status = new ClaudeBackendProbe(Factory(MustNotBeCalled(), keys), _time).Status(Allowed());

            run.FailureCode.ShouldBe(AgentFailureCodes.BackendUnavailable);
            ApiKeyHeader(stub.Requests.Single().Headers).ShouldBe(sentinel);
            var texts = harness.Events.Events.Select(item => item.Payload)
                .Concat(logger.Entries.Select(entry => entry.Text))
                .Append(run.FailureMessage ?? string.Empty)
                .Append(status.ToString())
                .Append(keys.Resolve().ToString());
            foreach (var form in ClaudeKeySentinels.SearchForms(sentinel))
            {
                texts.ShouldAllBe(text => !text.Contains(form, StringComparison.Ordinal));
            }
        }
    }

    private AgentStatusService StatusService(IClaudeKeyProvider keys)
    {
        var probes = new BackendProbes
        {
            Ollama = new OllamaBackendProbe(new StubHttpHandler { Failure = new HttpRequestException("down") }.Client(), _time),
            Claude = new ClaudeBackendProbe(Factory(MustNotBeCalled(), keys), _time),
        };
        var catalog = new ToolCatalog(new AgentToolRegistry([new MetaToolProvider()]), new ServiceStatusReader(Substitute.For<IGenerationActions>(), _time));
        return new AgentStatusService(new InMemorySettingsStore(Allowed()), Substitute.For<IGpuActivity>(), probes, catalog);
    }

    private static ClaudeKeyResolution Resolution(ClaudeApiKey key, ClaudeKeySourceDto source) => new()
    {
        Key = key,
        Source = source,
        State = source == ClaudeKeySourceDto.Stored ? ClaudeKeyStateDto.Stored : ClaudeKeyStateDto.Environment,
        LastFour = key.LastFour,
        Stored = StoredClaudeKeyStateDto.Readable,
        StoreAvailable = true,
    };

    private static object? Sdk(Microsoft.Extensions.AI.IChatClient client) => client.GetService(typeof(Anthropic.IAnthropicClient));

    private static ChatTurnContext Context(AgentRun run) => new(run, new PageContextDto { Page = "tracks" });

    private static AgentRun NewRun() => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserMessageId = Guid.NewGuid(),
        Status = AgentRunStatusDto.Running,
        Backend = AgentBackendDto.Claude,
        Model = "m",
        Tainted = false,
        ModelTurns = 0,
        ToolCalls = 0,
        InvalidCalls = 0,
        StartedAt = RunnerHarness.Start,
    };
}
