using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Library;

/// <summary>Rename, delete and retry of library items (feature 007, US3, FR-008, FR-010); the owner keeps the list.</summary>
public sealed partial class AssetTable
{
    private ElementReference _table;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IConfirmService Confirm { get; set; } = default!;

    /// <summary>The items in display order.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<AssetDto> Items { get; set; } = [];

    /// <summary>The item to mark (<c>aria-current</c>), e.g. from a confirmation link.</summary>
    [Parameter]
    public Guid? Highlight { get; set; }

    /// <summary>True to show each item's folder path (feature 022).</summary>
    [Parameter]
    public bool ShowPath { get; set; }

    /// <summary>The search hits of the items (feature 022), or null outside the search.</summary>
    [Parameter]
    public IReadOnlyDictionary<Guid, StemStudio.Contracts.Library.ItemHitDto>? Hits { get; set; }

    /// <summary>A screen-reader caption of the table, or null.</summary>
    [Parameter]
    public string? Caption { get; set; }

    /// <summary>An item changed (renamed, retried): the owner replaces it in its list.</summary>
    [Parameter]
    public EventCallback<AssetDto> OnReplaced { get; set; }

    /// <summary>An item was deleted: the owner removes it.</summary>
    [Parameter]
    public EventCallback<AssetDto> OnDeleted { get; set; }

    /// <summary>The list changed elsewhere (sheet music made, an item moved by library info): the owner reloads and polls.</summary>
    [Parameter]
    public EventCallback OnReload { get; set; }

    /// <summary>The item being renamed, or null.</summary>
    private AssetDto? Renaming { get; set; }

    private string? RenameError { get; set; }

    /// <summary>Why the last delete or retry failed.</summary>
    private string? ActionError { get; set; }

    /// <summary>Moves focus to the table (Arrow Down from the search box when no folder matched).</summary>
    /// <returns>A task that completes when focus moved.</returns>
    public ValueTask FocusAsync() => _table.FocusAsync();

    private StemStudio.Contracts.Library.ItemHitDto? HitOf(AssetDto asset) => Hits?.GetValueOrDefault(asset.Id);

    private void OpenRename(AssetDto asset) => (Renaming, RenameError) = (asset, null);

    private void CloseRename() => Renaming = null;

    private async Task RenameAsync(string name)
    {
        try
        {
            var renamed = await Api.RenameAssetAsync(Renaming!.Id, name);
            Renaming = null;
            await OnReplaced.InvokeAsync(renamed);
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
                await Api.DeleteAssetAsync(asset.Id);
                await OnDeleted.InvokeAsync(asset);
            });
        }
    }

    // "Make again" posts the recipe unchanged (FR-014); the new item appears after the reload.
    private Task MakeAgainAsync(AssetDto asset) => RunAsync(async () =>
    {
        await Api.CreateHandoutAsync(StemStudio.Web.Handouts.HandoutRecipes.Request(asset.Provenance.HandoutRecipe!));
        await OnReload.InvokeAsync();
    });

    private Task RetryAsync(AssetDto asset) => RunAsync(async () => await OnReplaced.InvokeAsync(await Api.RetryAssetAsync(asset.Id)));

    private async Task RunAsync(Func<Task> action) => ActionError = await ApiCall.RunAsync(action);
}
