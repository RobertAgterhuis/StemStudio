namespace StemStudio.Contracts.Styles;

/// <summary>A style of the library (1.8.0).</summary>
public sealed record StyleDto : StyleDefinition
{
    /// <summary>Style id (stable for presets).</summary>
    public required Guid Id { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last change (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
