using Microsoft.Extensions.DependencyInjection;

namespace StemStudio.Agent;

/// <summary>Registration of the agent library in the API host (skeleton; the ai-agent role fills it, T013-T069).</summary>
public static class AgentServiceCollectionExtensions
{
    /// <summary>
    /// Registers the agent services (<c>IAgentRunService</c>, <c>IAgentStatusService</c>, <c>ILlmGpuRelease</c>, the tool
    /// registry and the backends). The host supplies the ports it implements (stores, actions, GPU activity, event sink,
    /// UI gateway). Skeleton: registers nothing yet.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddStemStudioAgent(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
