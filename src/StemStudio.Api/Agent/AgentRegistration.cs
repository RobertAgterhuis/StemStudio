using Microsoft.Extensions.DependencyInjection.Extensions;
using StemStudio.Agent;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Gpu;
using StemStudio.Api.Agent.Loopback;
using StemStudio.Api.Agent.Stores;

namespace StemStudio.Api.Agent;

/// <summary>Registers the composer agent (feature 011): the agent library and the backend's port implementations.</summary>
public static class AgentRegistration
{
    /// <summary>Adds the agent library, the EF stores, the event hub and the start-up recovery.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration (<c>StemStudio:Agent</c>).</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAgentHost(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentHostOptions>(configuration.GetSection(AgentHostOptions.SectionName));
        services.AddStemStudioAgent();
        services.AddAgentStores().AddAgentRuntime().AddAgentEndpointServices();
        services.TryAddSingleton<IEnvironmentReader, ProcessEnvironmentReader>();
        services.AddLoopbackActions(configuration);
        services.AddGpuCoordination();
        services.DecorateGpuJobClients();
        return services;
    }

    // The ports are registered with plain Add after AddStemStudioAgent(), so these registrations win.
    private static IServiceCollection AddAgentStores(this IServiceCollection services)
    {
        services.AddSingleton<AgentDb>();
        services.AddSingleton<IConversationStore, EfConversationStore>();
        services.AddSingleton<IRunStore, EfRunStore>();
        services.AddSingleton<IUndoStore, EfUndoStore>();
        services.AddSingleton<IAgentSettingsStore, EfAgentSettingsStore>();
        services.AddSingleton<RunEventHub>();
        services.AddSingleton<IRunEventSink>(sp => sp.GetRequiredService<RunEventHub>());
        services.AddSingleton<IRunEventLogReader>(sp => sp.GetRequiredService<RunEventHub>());
        return services;
    }

    private static IServiceCollection AddAgentRuntime(this IServiceCollection services)
    {
        services.AddSingleton<AgentStartupRecovery>();
        services.AddSingleton<AgentQueries>();
        services.AddSingleton<AgentRunLifecycle>();
        services.AddSingleton<AgentRunFactory>();
        services.AddSingleton<SseWriter>();
        services.AddSingleton<AgentRunExecutor>();
        services.AddHostedService(sp => sp.GetRequiredService<AgentRunExecutor>());
        services.AddSingleton<AgentStatusProbe>();
        return services;
    }

    private static IServiceCollection AddAgentEndpointServices(this IServiceCollection services)
    {
        services.AddScoped<AgentConversationService>();
        services.AddScoped<AgentMessageService>();
        services.AddScoped<AgentRunEndpointService>();
        services.AddScoped<AgentApprovalService>();
        services.AddScoped<AgentSettingsService>();
        services.AddScoped<AgentUndoService>();
        return services;
    }
}
