using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.LibraryCare;

namespace StemStudio.Web.Components.Care;

/// <summary>Sorts the per-track table: largest total first; a header sorts by its column, a second press reverses it.</summary>
public sealed partial class StorageTracksTable
{
    /// <summary>The tracks of the overview.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<TrackStorageDto> Tracks { get; set; } = [];

    /// <summary>Per-row actions (the clean-up of a track's detail stems, Advanced+), or null for none.</summary>
    [Parameter]
    public RenderFragment<TrackStorageDto>? Actions { get; set; }

    private string SortColumn { get; set; } = "total";

    private bool Descending { get; set; } = true;

    private IReadOnlyList<TrackStorageDto> Sorted => StorageView.Sort(Tracks, SortColumn, Descending);

    private string AriaSort(string column) => column != SortColumn ? "none" : Descending ? "descending" : "ascending";

    private string SortIcon(string column) => column != SortColumn ? "unfold_more" : Descending ? "arrow_downward" : "arrow_upward";

    private void SortBy(string column) =>
        (SortColumn, Descending) = column == SortColumn ? (column, !Descending) : (column, true);
}
