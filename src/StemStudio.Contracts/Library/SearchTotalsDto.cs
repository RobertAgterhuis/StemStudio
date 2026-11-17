namespace StemStudio.Contracts.Library;

/// <summary>Hits per group in total (backend-api 1.22.0 <c>SearchTotals</c>).</summary>
public sealed record SearchTotalsDto
{
    /// <summary>Artist hits.</summary>
    public required int Artists { get; init; }

    /// <summary>Album hits.</summary>
    public required int Albums { get; init; }

    /// <summary>Song hits.</summary>
    public required int Songs { get; init; }

    /// <summary>Item hits.</summary>
    public required int Items { get; init; }
}
