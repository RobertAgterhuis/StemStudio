using StemStudio.Contracts;
using StemStudio.Contracts.Assets;

namespace StemStudio.Web.Services;

/// <summary>A source track of library items for the track filter.</summary>
/// <param name="Id">The track id.</param>
/// <param name="Label">The title, with "(deleted)" when the track no longer exists.</param>
public sealed record AssetTrackOption(Guid Id, string Label);

/// <summary>Client-side filter of the library (feature 007, FR-007): source track, kind and a name search.</summary>
public sealed class AssetFilter
{
    /// <summary>Message for an empty or too long item name.</summary>
    public const string NameMessage = "Enter a name of 1 to 120 characters.";

    /// <summary>Only items made from this track.</summary>
    public Guid? TrackId { get; set; }

    /// <summary>Only items of this kind.</summary>
    public AssetKindDto? Kind { get; set; }

    /// <summary>Case-insensitive "name contains".</summary>
    public string Search { get; set; } = string.Empty;

    /// <summary>True when any filter narrows the list.</summary>
    public bool IsActive => TrackId is not null || Kind is not null || Search.Trim().Length > 0;

    /// <summary>The distinct source tracks of the items, by label.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The options.</returns>
    public static IReadOnlyList<AssetTrackOption> Tracks(IEnumerable<AssetDto> items) =>
    [
        .. items.Where(a => a.Provenance.TrackId is not null)
            .GroupBy(a => a.Provenance.TrackId!.Value)
            .Select(g => new AssetTrackOption(g.Key, Label(g.First().Provenance)))
            .OrderBy(o => o.Label, StringComparer.CurrentCultureIgnoreCase),
    ];

    /// <summary>Validates a new item name.</summary>
    /// <param name="name">The entered name.</param>
    /// <returns>A readable problem, or null.</returns>
    public static string? ValidateName(string name) =>
        name.Trim().Length is > 0 and <= ContractLimits.AssetNameMaxLength ? null : NameMessage;

    /// <summary>The items that pass every filter, in their order.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The visible items.</returns>
    public IReadOnlyList<AssetDto> Apply(IEnumerable<AssetDto> items) =>
    [
        .. items.Where(a => (TrackId is null || a.Provenance.TrackId == TrackId)
            && (Kind is null || a.Kind == Kind)
            && a.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)),
    ];

    private static string Label(ProvenanceDto provenance) =>
        provenance.TrackAvailable ? provenance.TrackTitle : $"{provenance.TrackTitle} (deleted)";
}
