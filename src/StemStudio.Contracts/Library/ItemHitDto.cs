using StemStudio.Contracts.Assets;

namespace StemStudio.Contracts.Library;

/// <summary>An item search hit (backend-api 1.22.0 <c>ItemHit</c>).</summary>
public sealed record ItemHitDto
{
    /// <summary>The item.</summary>
    public required AssetDto Asset { get; init; }

    /// <summary>Artist, album and song folder of the item (exactly 3).</summary>
    public IReadOnlyList<FolderRefDto> Path { get; init; } = [];

    /// <summary>Highlights (at most 32).</summary>
    public IReadOnlyList<HighlightDto> Highlights { get; init; } = [];

    /// <summary>Instrument labels that matched a word (at most 8).</summary>
    public IReadOnlyList<string> MatchedInstruments { get; init; } = [];
}
