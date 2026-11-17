using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Library;
using StemStudio.Web.Library;

namespace StemStudio.Web.Components.Library;

/// <summary>Library info of the song's track (feature 022, US4 scenario 1, FR-032): the view follows the items.</summary>
public sealed partial class FolderView
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>The track whose library info is edited, or null.</summary>
    private LibraryInfoTarget? Filing { get; set; }

    private void EditTrackInfo(Guid trackId)
    {
        // An item of the track that has no own names moves with the track: its new place is the folder to open.
        var follow = View?.Items.FirstOrDefault(a => a.Provenance.TrackId == trackId && a.Placement is { } p
            && p.Artist.Source != PlacementSourceDto.Item && p.Album.Source != PlacementSourceDto.Item && p.Song.Source != PlacementSourceDto.Item);
        Filing = new LibraryInfoTarget(LibraryInfoScope.Track, trackId, View?.Folder?.Name ?? string.Empty, follow?.Id);
    }

    private void CloseFiling() => Filing = null;

    private async Task OnFiledAsync(string? songFolderId)
    {
        Filing = null;
        if (songFolderId is not null && songFolderId != FolderId)
        {
            Navigation.NavigateTo(LibraryLocation.FolderUrl(songFolderId));
            return;
        }

        await LoadAsync(null);
    }
}
