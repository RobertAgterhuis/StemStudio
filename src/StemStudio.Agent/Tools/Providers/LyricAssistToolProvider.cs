namespace StemStudio.Agent.Tools.Providers;

/// <summary>
/// Tools of feature 023 (tool catalogue 1.2.0, <see cref="LyricAssistToolNames"/>). Skeleton of the 023 Phase 0: no descriptor yet
/// and not registered; the ai-agent adds the eight descriptors test-first (T054/T055) and registers the provider.
/// </summary>
public sealed class LyricAssistToolProvider : IAgentToolProvider
{
    /// <inheritdoc/>
    public IEnumerable<AgentToolDescriptor> GetTools() => [];
}
