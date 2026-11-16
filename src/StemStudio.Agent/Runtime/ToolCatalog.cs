using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>The registry together with the service states the runner last read (one per process, shared with the runner).</summary>
/// <param name="Registry">Tool registry.</param>
/// <param name="Status">Service status reader.</param>
public sealed record ToolCatalog(AgentToolRegistry Registry, ServiceStatusReader Status)
{
    /// <summary>The tool context of a page with the last known service states.</summary>
    /// <param name="page">Page context.</param>
    /// <returns>The context.</returns>
    public ToolContext Peek(PageContextDto page) => Status.Peek(page);

    /// <summary>The tools of a page with availability, sorted by name.</summary>
    /// <param name="page">Page context.</param>
    /// <returns>The tool infos.</returns>
    public IReadOnlyList<AgentToolInfoDto> List(PageContextDto page) => [.. Registry.Describe(Peek(page)).Select(offer => offer.ToInfo())];
}
