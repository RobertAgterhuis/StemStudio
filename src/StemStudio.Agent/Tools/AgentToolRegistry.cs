using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tools;

/// <summary>A selected tool with its availability (GET /api/agent/tools).</summary>
/// <param name="Descriptor">The tool.</param>
/// <param name="UnavailableReason">Why it is not offered now; null when available.</param>
public sealed record ToolOffer(AgentToolDescriptor Descriptor, string? UnavailableReason)
{
    /// <summary>True when the tool is offered to the model.</summary>
    public bool Available => UnavailableReason is null;

    /// <summary>The contract view of the offer.</summary>
    /// <returns>The DTO.</returns>
    public AgentToolInfoDto ToInfo() => new()
    {
        Name = Descriptor.Name,
        Feature = Descriptor.Feature,
        Kind = Descriptor.Kind,
        Risk = Descriptor.Risk,
        Undo = Descriptor.UndoKind,
        Available = Available,
        UnavailableReason = UnavailableReason,
        Title = Descriptor.Title,
        Description = Descriptor.Description,
    };
}

/// <summary>
/// All registered tools (one provider per feature) and the per-request selection: the page context's groups, minus the
/// unavailable tools, sorted by name so the tool list is byte-stable across turns (prompt caching, data-model 5).
/// </summary>
public sealed class AgentToolRegistry
{
    /// <summary>Maximum tools offered in one request (research section 4).</summary>
    public const int MaxToolsPerRequest = 32;

    private readonly Dictionary<string, AgentToolDescriptor> _byName = new(StringComparer.Ordinal);

    /// <summary>Registers the tools of every provider; rejects duplicate names and profiles above the cap.</summary>
    /// <param name="providers">The providers.</param>
    /// <exception cref="InvalidOperationException">A name is registered twice or a page offers more than 32 tools.</exception>
    public AgentToolRegistry(IEnumerable<IAgentToolProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        foreach (var tool in providers.SelectMany(provider => provider.GetTools()))
        {
            if (!_byName.TryAdd(tool.Name, tool))
            {
                throw new InvalidOperationException($"The agent tool '{tool.Name}' is registered twice.");
            }
        }

        All = [.. _byName.Values.OrderBy(tool => tool.Name, StringComparer.Ordinal)];
        CheckCap();
    }

    /// <summary>Every registered tool, sorted by name.</summary>
    public IReadOnlyList<AgentToolDescriptor> All { get; }

    /// <summary>The tool with the name, or null for unknown names.</summary>
    /// <param name="name">Tool name.</param>
    /// <returns>The descriptor.</returns>
    public AgentToolDescriptor? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>The tools offered to the model in the context (available only, sorted by name, at most 32).</summary>
    /// <param name="context">Page context and service states.</param>
    /// <returns>The tools.</returns>
    public IReadOnlyList<AgentToolDescriptor> For(ToolContext context) =>
        [.. Describe(context).Where(offer => offer.Available).Select(offer => offer.Descriptor)];

    /// <summary>The tools of the context's page profile with their availability.</summary>
    /// <param name="context">Page context and service states.</param>
    /// <returns>The offers, sorted by name.</returns>
    public IReadOnlyList<ToolOffer> Describe(ToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var profile = ToolSelection.For(context.Page);
        return [.. All.Where(profile.Selects).Select(tool => new ToolOffer(tool, UnavailableReason(tool, context)))];
    }

    private static string? UnavailableReason(AgentToolDescriptor tool, ToolContext context) => tool.Availability switch
    {
        ToolAvailability.Mixer => context.MixerOpen ? null : ToolContext.MixerClosedReason,
        ToolAvailability.GenerationService => ServiceReason(context.Generation),
        ToolAvailability.VoiceService => ServiceReason(context.Voice),
        _ => null,
    };

    private static string? ServiceReason(ServiceAvailability service) => service.Ready ? null : service.Reason ?? ServiceAvailability.UnknownReason;

    private void CheckCap()
    {
        foreach (var profile in ToolSelection.All)
        {
            var count = All.Count(profile.Selects);
            if (count > MaxToolsPerRequest)
            {
                throw new InvalidOperationException($"A page context offers {count} tools; the limit is {MaxToolsPerRequest}.");
            }
        }
    }
}
