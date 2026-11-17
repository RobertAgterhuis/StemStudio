using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Backends.Fakes;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;

namespace StemStudio.Agent;

/// <summary>Registration of the agent library in the API host.</summary>
public static class AgentServiceCollectionExtensions
{
    /// <summary>Environment variable with the Ollama base URL (compose: <c>http://host.docker.internal:11434</c>).</summary>
    public const string OllamaBaseUrlVariable = "StemStudio__Agent__Ollama__BaseUrl";

    /// <summary>Environment variable with the path of the fake backend's script (E2E stack only); blank counts as unset.</summary>
    public const string FakeScriptVariable = "STEMSTUDIO_AGENT_FAKE_SCRIPT";

    /// <summary>
    /// Registers the agent library: <see cref="IAgentRunService"/> (one <see cref="AgentRunService"/> singleton, which holds
    /// the approval waits and run cancellations in memory), <see cref="IAgentStatusService"/>, <see cref="IUiToolGateway"/>,
    /// <see cref="ILlmGpuRelease"/>, <see cref="IEnvironmentReader"/>, <see cref="IClaudeKeyProvider"/> (over the host's
    /// <see cref="IStoredClaudeKeyReader"/>, or a reader without a store when the host registers none), the tool registry, the backends (one Ollama
    /// <see cref="HttpClient"/>, Claude, the fake script session) and the model selector. Every registration uses
    /// <c>TryAdd</c>; the host registers the four stores, <see cref="IRunEventSink"/>, <see cref="IStemStudioActions"/> and
    /// <see cref="IGpuActivity"/> (an idle default is added) itself, before or after this call.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddStemStudioAgent(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IEnvironmentReader, ProcessEnvironmentReader>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IGpuActivity, IdleGpuActivity>();
        AddOllama(services);
        AddChat(services);
        AddTools(services);
        AddRuntime(services);
        return services;
    }

    private static void AddOllama(IServiceCollection services)
    {
        services.TryAddSingleton(sp => new OllamaConnection(sp.GetRequiredService<IEnvironmentReader>().GetVariable(OllamaBaseUrlVariable)));
        services.TryAddSingleton(sp => new OllamaChatClientFactory(Http(sp)));
        services.TryAddSingleton(sp => new OllamaBackendProbe(Http(sp), Time(sp)));
        services.TryAddSingleton<ILlmGpuRelease>(sp => new OllamaGpuRelease(Http(sp), sp.GetRequiredService<IAgentSettingsStore>(), Time(sp)));
    }

    private static void AddChat(IServiceCollection services)
    {
        services.TryAddSingleton<IClaudeKeyProvider>(sp => new ClaudeKeyProvider(
            sp.GetRequiredService<IEnvironmentReader>(), sp.GetService<IStoredClaudeKeyReader>() ?? NoStoredClaudeKeyReader.Instance));
        services.TryAddSingleton(sp => new ClaudeChatClientFactory(sp.GetRequiredService<IClaudeKeyProvider>()));
        services.TryAddSingleton(sp => new ClaudeBackendProbe(sp.GetRequiredService<ClaudeChatClientFactory>(), Time(sp)));
        services.TryAddSingleton(sp => new ChatBackends
        {
            Ollama = sp.GetRequiredService<OllamaChatClientFactory>(),
            Claude = new ClaudeChatSession(sp.GetRequiredService<IAgentSettingsStore>(), sp.GetRequiredService<ClaudeChatClientFactory>()),
            Fake = FakeSession(sp.GetRequiredService<IEnvironmentReader>()),
        });
        services.TryAddSingleton<IAgentChatSession>(sp =>
            new AgentModelSelector(sp.GetRequiredService<IAgentSettingsStore>(), sp.GetRequiredService<IGpuActivity>(), sp.GetRequiredService<ChatBackends>()));
    }

    private static void AddTools(IServiceCollection services)
    {
        AddProviders(services);
        services.TryAddSingleton(sp => new AgentToolRegistry(sp.GetServices<IAgentToolProvider>()));
        services.TryAddSingleton(sp => new ServiceStatusReader(sp.GetRequiredService<IStemStudioActions>().Generations, Time(sp)));
        services.TryAddSingleton(sp => new ToolCatalog(sp.GetRequiredService<AgentToolRegistry>(), sp.GetRequiredService<ServiceStatusReader>()));
    }

    private static void AddProviders(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, MetaToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, Core001ToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, Detail002ToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, Device003ToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, HelpToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, ArtistToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, LyricsToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, ExportToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, ScoreToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, StyleToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, SongToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProvider, LibraryToolProvider>());
    }

    private static void AddRuntime(IServiceCollection services)
    {
        services.TryAddSingleton<IUiToolGateway>(sp => new UiToolGateway(Time(sp)));
        services.TryAddSingleton(sp => new BackendProbes
        {
            Ollama = sp.GetRequiredService<OllamaBackendProbe>(),
            Claude = sp.GetRequiredService<ClaudeBackendProbe>(),
            FakeAvailable = sp.GetRequiredService<ChatBackends>().Fake is not null,
        });
        services.TryAddSingleton<IAgentStatusService>(sp => new AgentStatusService(
            sp.GetRequiredService<IAgentSettingsStore>(), sp.GetRequiredService<IGpuActivity>(), sp.GetRequiredService<BackendProbes>(), sp.GetRequiredService<ToolCatalog>()));
        services.TryAddSingleton(sp => new AgentRunService(Services(sp)));
        services.TryAddSingleton<IAgentRunService>(sp => sp.GetRequiredService<AgentRunService>());
    }

    private static AgentServices Services(IServiceProvider sp) => new()
    {
        Runs = sp.GetRequiredService<IRunStore>(),
        Conversations = sp.GetRequiredService<IConversationStore>(),
        Settings = sp.GetRequiredService<IAgentSettingsStore>(),
        Events = sp.GetRequiredService<IRunEventSink>(),
        Actions = sp.GetRequiredService<IStemStudioActions>(),
        Ui = sp.GetRequiredService<IUiToolGateway>(),
        Registry = sp.GetRequiredService<AgentToolRegistry>(),
        Chat = sp.GetRequiredService<IAgentChatSession>(),
        Undo = sp.GetRequiredService<IUndoStore>(),
        ServiceStatus = sp.GetRequiredService<ServiceStatusReader>(),
        Time = Time(sp),
        Logger = sp.GetService<ILoggerFactory>()?.CreateLogger("StemStudio.Agent") ?? NullLogger.Instance,
    };

    /// <summary>The scripted session of the fake backend, or null when no readable script is configured.</summary>
    private static ScriptedChatSession? FakeSession(IEnvironmentReader environment)
    {
        var path = environment.GetVariable(FakeScriptVariable);
        return string.IsNullOrWhiteSpace(path) || ReadScript(path.Trim()) is not { } script ? null : new ScriptedChatSession(script);
    }

    private static string? ReadScript(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static HttpClient Http(IServiceProvider sp) => sp.GetRequiredService<OllamaConnection>().Http;

    private static TimeProvider Time(IServiceProvider sp) => sp.GetRequiredService<TimeProvider>();
}
