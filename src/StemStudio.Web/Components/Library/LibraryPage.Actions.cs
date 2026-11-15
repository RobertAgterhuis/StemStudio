using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Library;

/// <summary>Rename, delete and retry of library items (feature 007, US3, FR-008, FR-010).</summary>
public sealed partial class LibraryPage
{
    [Inject]
    private IConfirmService Confirm { get; set; } = default!;

    /// <summary>The item being renamed, or null.</summary>
    private AssetDto? Renaming { get; set; }

    private string? RenameError { get; set; }

    /// <summary>Why the last delete or retry failed.</summary>
    private string? ActionError { get; set; }

    private void OpenRename(AssetDto asset) => (Renaming, RenameError) = (asset, null);

    private void CloseRename() => Renaming = null;

    private async Task RenameAsync(string name)
    {
        try
        {
            Replace(await Api.RenameAssetAsync(Renaming!.Id, name, Stopping));
            Renaming = null;
        }
        catch (ApiException ex)
        {
            RenameError = ex.Error.UserMessage;
        }
    }

    private async Task DeleteAsync(AssetDto asset)
    {
        var message = $"Delete \"{asset.Name}\" from the library? Its file is removed; the track stays.";
        if (await Confirm.ConfirmAsync("Delete item", message, "Delete"))
        {
            await RunAsync(async () =>
            {
                await Api.DeleteAssetAsync(asset.Id, Stopping);
                Items = [.. Items!.Where(a => a.Id != asset.Id)];
                TotalSize = Math.Max(0, TotalSize - asset.SizeBytes);
            });
        }
    }

    private Task RetryAsync(AssetDto asset) => RunAsync(async () =>
    {
        Replace(await Api.RetryAssetAsync(asset.Id, Stopping));
        StartPolling();
    });

    private void Replace(AssetDto asset) => Items = [.. Items!.Select(a => a.Id == asset.Id ? asset : a)];

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            ActionError = null;
        }
        catch (ApiException ex)
        {
            ActionError = ex.Error.UserMessage;
        }
    }
}
