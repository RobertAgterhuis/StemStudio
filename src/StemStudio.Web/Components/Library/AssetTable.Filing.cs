using StemStudio.Contracts.Assets;
using StemStudio.Web.Library;

namespace StemStudio.Web.Components.Library;

/// <summary>Library info of one item (feature 022, US4 scenario 2): the dialog, then the owner reloads (the item moved).</summary>
public sealed partial class AssetTable
{
    /// <summary>The item whose library info is edited, or null.</summary>
    private LibraryInfoTarget? Filing { get; set; }

    private void OpenLibraryInfo(AssetDto asset) => Filing = new LibraryInfoTarget(LibraryInfoScope.Item, asset.Id, asset.Name);

    private void CloseLibraryInfo() => Filing = null;

    private async Task OnLibraryInfoSavedAsync(string? songFolderId)
    {
        Filing = null;
        await OnReload.InvokeAsync();
    }
}
