using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Backends.Fakes;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests;

/// <summary>
/// <c>AddStemStudioAgent()</c> (integration requirements of the backend lane): the inbound ports and agent services, one
/// <see cref="AgentRunService"/>, one Ollama <see cref="HttpClient"/>, and host ports that the backend registers afterwards.
/// </summary>
public sealed class AgentServiceCollectionExtensionsTests : IDisposable
{
    private readonly string _script = Path.Combine(Path.GetTempPath(), $"agent-script-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_script);

    [Fact]
    public void AddStemStudioAgent_ReturnsTheSameCollection()
    {
        var services = new ServiceCollection();

        services.AddStemStudioAgent().ShouldBeSameAs(services);
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection)null!).AddStemStudioAgent());
    }

    [Fact]
    public void RegistersTheInboundPorts_AndExactlyOneRunService()
    {
        var services = Host(new FakeEnvironment());

        services.Count(descriptor => descriptor.ServiceType == typeof(IAgentRunService)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(AgentRunService)).ShouldBe(1);
        using var provider = new TestServiceProvider(services);
        provider.ResolveAll();
        var run = provider.GetRequiredService<IAgentRunService>();
        run.ShouldBeOfType<AgentRunService>();
        provider.GetRequiredService<IAgentRunService>().ShouldBeSameAs(run);
        provider.GetRequiredService<AgentRunService>().ShouldBeSameAs(run);
        provider.GetRequiredService<IAgentStatusService>().ShouldBeOfType<AgentStatusService>();
        provider.GetRequiredService<IUiToolGateway>().ShouldBeOfType<UiToolGateway>();
        provider.GetRequiredService<ILlmGpuRelease>().ShouldBeOfType<OllamaGpuRelease>();
        provider.GetRequiredService<IAgentChatSession>().ShouldBeOfType<AgentModelSelector>();
        provider.GetRequiredService<AgentToolRegistry>().All.ShouldNotBeEmpty();
    }

    [Fact]
    public void TheEnvironmentReader_IsTheProcessReader_UnlessTheHostReplacesIt()
    {
        var plain = new ServiceCollection().AddStemStudioAgent();
        using var provider = new TestServiceProvider(plain);
        provider.GetRequiredService<IEnvironmentReader>().ShouldBeOfType<ProcessEnvironmentReader>();

        var replaced = Host(FakeEnvironment.WithKey());
        using var host = new TestServiceProvider(replaced);
        host.GetRequiredService<IEnvironmentReader>().ShouldBeOfType<FakeEnvironment>();
    }

    [Fact]
    public void ProcessReader_TreatsBlankAsUnset()
    {
        var name = $"STEMSTUDIO_TEST_{Guid.NewGuid():N}";
        var reader = new ProcessEnvironmentReader();
        reader.GetVariable(name).ShouldBeNull();
        Environment.SetEnvironmentVariable(name, "   ");
        try
        {
            reader.GetVariable(name).ShouldBeNull();
            Environment.SetEnvironmentVariable(name, "value");
            reader.GetVariable(name).ShouldBe("value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void HostPorts_RegisteredAfterwards_Win_AndDefaultsUseTryAdd()
    {
        var services = new ServiceCollection().AddStemStudioAgent();
        var gpu = Substitute.For<IGpuActivity>();
        services.AddSingleton(gpu);

        services.Where(descriptor => descriptor.ServiceType == typeof(IRunStore) || descriptor.ServiceType == typeof(IStemStudioActions)
            || descriptor.ServiceType == typeof(IRunEventSink) || descriptor.ServiceType == typeof(IConversationStore)
            || descriptor.ServiceType == typeof(IUndoStore) || descriptor.ServiceType == typeof(IAgentSettingsStore)).ShouldBeEmpty();
        using var provider = new TestServiceProvider(services);
        provider.GetRequiredService<IGpuActivity>().ShouldBeSameAs(gpu);
    }

    [Fact]
    public void WithoutAHostGpuActivity_TheGpuCountsAsIdle()
    {
        using var provider = new TestServiceProvider(new ServiceCollection().AddStemStudioAgent());

        provider.GetRequiredService<IGpuActivity>().IsHeavyJobActive().ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, "http://localhost:11434/")]
    [InlineData("http://host.docker.internal:11434", "http://host.docker.internal:11434/")]
    [InlineData("  ", "http://localhost:11434/")]
    public void OneOllamaClient_WithATrailingSlash_AndNoHttpClientTimeout(string? configured, string expected)
    {
        var environment = configured is null ? new FakeEnvironment() : new FakeEnvironment((AgentServiceCollectionExtensions.OllamaBaseUrlVariable, configured));
        using var provider = new TestServiceProvider(Host(environment));

        var connection = provider.GetRequiredService<OllamaConnection>();

        connection.Http.BaseAddress.ShouldBe(new Uri(expected));
        connection.Http.Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
        provider.GetRequiredService<OllamaChatClientFactory>().BaseAddress.ShouldBe(new Uri(expected));
    }

    [Fact]
    public void TheFakeBackendPlaysTheScript_FromTheFileInTheEnvironment()
    {
        File.WriteAllText(_script, """{"version":1,"turns":[{"text":"ok"}]}""");
        using var provider = new TestServiceProvider(Host(new FakeEnvironment((AgentServiceCollectionExtensions.FakeScriptVariable, _script))));

        provider.GetRequiredService<ChatBackends>().Fake.ShouldBeOfType<ScriptedChatSession>();
        provider.GetRequiredService<BackendProbes>().FakeAvailable.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankFakeScript_CountsAsUnset(string value)
    {
        using var provider = new TestServiceProvider(Host(new FakeEnvironment((AgentServiceCollectionExtensions.FakeScriptVariable, value))));

        provider.GetRequiredService<ChatBackends>().Fake.ShouldBeNull();
        provider.GetRequiredService<BackendProbes>().FakeAvailable.ShouldBeFalse();
    }

    [Fact]
    public void AMissingFakeScriptFile_LeavesTheFakeBackendUnavailable()
    {
        using var provider = new TestServiceProvider(Host(new FakeEnvironment((AgentServiceCollectionExtensions.FakeScriptVariable, _script + ".missing"))));

        provider.GetRequiredService<ChatBackends>().Fake.ShouldBeNull();
    }

    [Fact]
    public void TheClaudeBackend_IsAlwaysWired_ButOnlyUsableWithTheKey()
    {
        using var provider = new TestServiceProvider(Host(new FakeEnvironment()));

        provider.GetRequiredService<ChatBackends>().Claude.ShouldBeOfType<ClaudeChatSession>();
        provider.GetRequiredService<ClaudeChatClientFactory>().KeyConfigured.ShouldBeFalse();
    }

    [Fact]
    public async Task AResolvedRunService_ExecutesAFakeRun()
    {
        File.WriteAllText(_script, """{"version":1,"turns":[{"text":"Klaar."}]}""");
        var harness = RunnerHarness.Scripted([TestTools.Echo()]);
        var services = Host(new FakeEnvironment((AgentServiceCollectionExtensions.FakeScriptVariable, _script)), harness);
        using var provider = new TestServiceProvider(services);
        await harness.SeedAsync();

        await provider.GetRequiredService<IAgentRunService>().ExecuteAsync(harness.Run.Id, TestContext.Current.CancellationToken);

        var run = await harness.CurrentRunAsync();
        run!.Status.ShouldBe(AgentRunStatusDto.Completed);
        harness.Events.Types.ShouldContain(AgentEventTypes.RunCompleted);
    }

    /// <summary>The agent library plus the host's ports, registered after it like the backend does.</summary>
    private static IServiceCollection Host(IEnvironmentReader environment, RunnerHarness? harness = null)
    {
        harness ??= RunnerHarness.Scripted([]);
        var services = new ServiceCollection().AddStemStudioAgent();
        services.AddSingleton(environment);
        services.AddSingleton<IRunStore>(harness.Runs);
        services.AddSingleton<IConversationStore>(harness.Conversations);
        services.AddSingleton<IUndoStore>(harness.Undo);
        services.AddSingleton<IAgentSettingsStore>(harness.Settings);
        services.AddSingleton<IRunEventSink>(harness.Events);
        services.AddSingleton(harness.Actions);
        services.AddSingleton(Substitute.For<IGpuActivity>());
        services.AddSingleton<TimeProvider>(harness.Time);
        return services;
    }
}
