using StemStudio.Contracts.Generation;

namespace StemStudio.Agent.Ports;

/// <summary>Filters of GET /api/generations used by list_generations.</summary>
public sealed record GenerationQuery
{
    /// <summary>Only restyles, songs or voice conversions.</summary>
    public GenerationKindDto? Kind { get; init; }

    /// <summary>Only generations made from this track (<c>sourceTrackId</c>).</summary>
    public Guid? SourceTrackId { get; init; }
}
