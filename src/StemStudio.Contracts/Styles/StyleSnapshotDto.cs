namespace StemStudio.Contracts.Styles;

/// <summary>A style as it was when a generation was created (1.8.0).</summary>
public sealed record StyleSnapshotDto : StyleDefinition
{
    /// <summary>The style's id.</summary>
    public required Guid StyleId { get; init; }
}
