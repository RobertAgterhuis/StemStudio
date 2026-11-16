using StemStudio.Agent;
using StemStudio.Agent.Ports;
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
        services.AddSingleton<AgentDb>();
        services.AddSingleton<IConversationStore, EfConversationStore>();
        services.AddSingleton<IRunStore, EfRunStore>();
        services.AddSingleton<IUndoStore, EfUndoStore>();
        services.AddSingleton<IAgentSettingsStore, EfAgentSettingsStore>();
        services.AddSingleton<RunEventHub>();
        services.AddSingleton<IRunEventSink>(sp => sp.GetRequiredService<RunEventHub>());
        services.AddSingleton<IRunEventLogReader>(sp => sp.GetRequiredService<RunEventHub>());
        services.AddSingleton<AgentStartupRecovery>();
        return services;
    }
}
