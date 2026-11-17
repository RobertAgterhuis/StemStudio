using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Library;

namespace StemStudio.Web.Components.Library;

/// <summary>Parameters, visible hits and focus of a group of folder hits.</summary>
public sealed partial class SearchGroup
{
    private ElementReference _first;

    /// <summary>The group id: <c>artists</c>, <c>albums</c> or <c>songs</c>.</summary>
    [Parameter, EditorRequired]
    public string Group { get; set; } = string.Empty;

    /// <summary>The heading, e.g. "Artists".</summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>The hits the API returned.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<FolderHitDto> Hits { get; set; } = [];

    /// <summary>The number of hits in the library.</summary>
    [Parameter]
    public int Total { get; set; }

    /// <summary>How many hits to show (20 until "Show all").</summary>
    [Parameter]
    public int Limit { get; set; } = StemStudio.Contracts.ContractLimits.LibrarySearchGroupDefault;

    /// <summary>"Show all N" pressed.</summary>
    [Parameter]
    public EventCallback OnShowAll { get; set; }

    private string HeadingId => $"search-heading-{Group}";

    private IReadOnlyList<FolderHitDto> Shown => [.. Hits.Take(Limit)];

    /// <summary>Moves focus to the first hit.</summary>
    /// <returns>A task that completes when focus moved.</returns>
    public ValueTask FocusFirstAsync() => _first.FocusAsync();
}
