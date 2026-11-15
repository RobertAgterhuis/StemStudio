namespace StemStudio.Contracts.Styles;

/// <summary>Style fields plus the kind and the preset key (backend contract 1.8.0); base of <see cref="StyleDto"/> and <see cref="StyleSnapshotDto"/>.</summary>
public abstract record StyleDefinition : StyleFields
{
    /// <summary>Preset or user style.</summary>
    public required StyleKindDto Kind { get; init; }

    /// <summary>Stable key of a preset, e.g. <c>jazz-trio</c>; null for user styles.</summary>
    public string? PresetKey { get; init; }
}
