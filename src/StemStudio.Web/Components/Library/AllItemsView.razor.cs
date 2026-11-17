using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Library;

/// <summary>Loads every item, keeps them newest first and polls while an item is queued or rendering (feature 007).</summary>
public sealed partial class AllItemsView
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The item to highlight (<c>?asset=</c>, from an older confirmation link).</summary>
    [Parameter]
    public Guid? Highlight { get; set; }

    private IReadOnlyList<AssetDto>? Items { get; set; }

    private long TotalSize { get; set; }

    private string? Error { get; set; }

    private AssetFilter Filter { get; } = new();

    private IReadOnlyList<AssetDto> Visible => Filter.Apply(Items ?? []);

    private string EmptyText => Filter.IsActive && Items is { Count: > 0 }
        ? "No items match the filters."
        : "No items yet. Save a stem on a track page or what you hear in the mixer.";

    /// <inheritdoc />
    protected override bool NeedsPolling => Items?.Any(a => a.Status is AssetStatusDto.Queued or AssetStatusDto.Rendering) == true;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        StartPolling();
    }

    /// <inheritdoc />
    protected override Task RefreshAsync(CancellationToken ct) => LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            var list = await Api.ListAssetsAsync(Stopping);
            (Items, TotalSize, Error) = (list.Items.OrderByDescending(a => a.CreatedAt).ToList(), list.TotalSizeBytes, null);
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    private void Replace(AssetDto asset)
    {
        Items = [.. Items!.Select(a => a.Id == asset.Id ? asset : a)];
        StartPolling();
    }

    private void Remove(AssetDto asset)
    {
        Items = [.. Items!.Where(a => a.Id != asset.Id)];
        TotalSize = Math.Max(0, TotalSize - asset.SizeBytes);
    }

    private async Task OnReloadAsync()
    {
        await LoadAsync();
        StartPolling();
    }
}
