namespace StemStudio.Contracts.Library;

/// <summary>Where a library item is filed (backend-api 1.22.0 <c>Placement</c>, member <c>Asset.placement</c>).</summary>
public sealed record PlacementDto
{
    /// <summary>Artist level.</summary>
    public required PlacementLevelDto Artist { get; init; }

    /// <summary>Album level.</summary>
    public required PlacementLevelDto Album { get; init; }

    /// <summary>Song level.</summary>
    public required PlacementLevelDto Song { get; init; }

    /// <summary>Album year (1000..2999) from the match; null otherwise.</summary>
    public int? Year { get; init; }

    /// <summary>False only until the start-up filing reached an older item (the names are then the automatic preview).</summary>
    public required bool Filed { get; init; }
}
