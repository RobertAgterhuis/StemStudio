using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Library;

/// <summary>Loads the library, keeps it newest first and polls while an item is queued or rendering (feature 007).</summary>
public sealed partial class LibraryPage
{

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The item to highlight (<c>?asset=</c>, from the export confirmation).</summary>
    [SupplyParameterFromQuery(Name = "asset")]
    public Guid? Highlight { get; set; }

    private IReadOnlyList<AssetDto>? Items { get; set; }

    private long TotalSize { get; set; }

    private string? Error { get; set; }

    private IReadOnlyList<AssetDto> Visible => Items ?? [];

    private const string EmptyText = "No items yet. Save a stem on a track page or what you hear in the mixer.";

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
}
