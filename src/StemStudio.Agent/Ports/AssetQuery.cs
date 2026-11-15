using StemStudio.Contracts.Assets;

namespace StemStudio.Agent.Ports;

/// <summary>Filters of GET /api/assets used by list_exports and list_scores.</summary>
public sealed record AssetQuery
{
    /// <summary>Only audio or score items.</summary>
    public AssetKindDto? Kind { get; init; }

    /// <summary>Only items made from this track.</summary>
    public Guid? TrackId { get; init; }

    /// <summary>Only items in this status.</summary>
    public AssetStatusDto? Status { get; init; }

    /// <summary>Case-insensitive "name contains".</summary>
    public string? Query { get; init; }

    /// <summary>Page size.</summary>
    public int? Limit { get; init; }
}
