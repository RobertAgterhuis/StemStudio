using Microsoft.AspNetCore.Components;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;

namespace StemStudio.Web.Components.Library;

/// <summary>Groups of a search result, "Show all" per group and focus of the first result.</summary>
public sealed partial class SearchResults
{
    private const string ItemsGroup = "items";

    private readonly Dictionary<string, SearchGroup> _groups = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private AssetTable? _items;

    /// <summary>The newest result, or null while the first search runs.</summary>
    [Parameter]
    public LibrarySearchResultDto? Result { get; set; }

    /// <summary>Why the last search failed, or null.</summary>
    [Parameter]
    public string? Error { get; set; }

    /// <summary>"Clear search" pressed.</summary>
    [Parameter]
    public EventCallback OnClear { get; set; }

    /// <summary>"Show all" pressed: the page asks for the largest groups (the result then holds up to 200 per group).</summary>
    [Parameter]
    public EventCallback OnShowAll { get; set; }

    /// <summary>An item changed in the results (renamed, deleted, retried, sheet music made): the page searches again.</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    private bool IsEmpty => Result is { Artists.Count: 0, Albums.Count: 0, Songs.Count: 0, Items.Count: 0 };

    private IEnumerable<(string Group, string Title, IReadOnlyList<FolderHitDto> Hits, int Total)> FolderGroups =>
    [
        ("artists", "Artists", Result!.Artists, Result.Totals.Artists),
        ("albums", "Albums", Result.Albums, Result.Totals.Albums),
        ("songs", "Songs", Result.Songs, Result.Totals.Songs),
    ];

    private IReadOnlyList<AssetDto> ShownItems => [.. Result!.Items.DistinctBy(hit => hit.Asset.Id).Take(LimitOf(ItemsGroup)).Select(hit => hit.Asset)];

    private Dictionary<Guid, ItemHitDto> ItemHits => Result!.Items.DistinctBy(hit => hit.Asset.Id).ToDictionary(hit => hit.Asset.Id);

    /// <summary>Moves focus to the first result (Arrow Down from the search box).</summary>
    /// <returns>A task that completes when focus moved.</returns>
    public async Task FocusFirstAsync()
    {
        if (Result is null || IsEmpty)
        {
            return;
        }

        var first = FolderGroups.Where(g => g.Hits.Count > 0).Select(g => _groups.GetValueOrDefault(g.Group)).FirstOrDefault(g => g is not null);

        if (first is not null)
        {
            await first.FocusFirstAsync();
        }
        else if (_items is not null)
        {
            await _items.FocusAsync();
        }
    }

    private Task Changed(AssetDto asset) => OnChanged.InvokeAsync();

    private int LimitOf(string group) => _expanded.Contains(group) ? ContractLimits.LibrarySearchGroupMax : ContractLimits.LibrarySearchGroupDefault;

    private async Task ShowAllAsync(string group)
    {
        _expanded.Add(group);
        await OnShowAll.InvokeAsync();
    }
}
