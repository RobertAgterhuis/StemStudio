namespace StemStudio.Agent.Tools;

/// <summary>Contributes the tools of one feature to the registry (one provider per feature, data-model 5).</summary>
public interface IAgentToolProvider
{
    /// <summary>The descriptors of the feature.</summary>
    /// <returns>The descriptors.</returns>
    IEnumerable<AgentToolDescriptor> GetTools();
}
