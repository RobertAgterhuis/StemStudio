using StemStudio.Contracts.Styles;

namespace StemStudio.Agent.Ports;

/// <summary>Filters of GET /api/styles used by list_styles.</summary>
public sealed record StyleQuery
{
    /// <summary>Case-insensitive search text (<c>q</c>).</summary>
    public string? Query { get; init; }

    /// <summary>Only presets or only user styles.</summary>
    public StyleKindDto? Kind { get; init; }

    /// <summary>Only styles of this genre family.</summary>
    public GenreFamilyDto? GenreFamily { get; init; }
}
