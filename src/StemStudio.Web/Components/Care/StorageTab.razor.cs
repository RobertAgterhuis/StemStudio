using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.LibraryCare;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Care;

/// <summary>Loads the storage overview (cached by the API for 30 s) and measures again on Refresh (013 US3).</summary>
public sealed partial class StorageTab
{
    [Inject]
    private ILibraryCareClient Client { get; set; } = default!;

    /// <summary>The library care status (null before the first answer).</summary>
    [Parameter]
    public LibraryCareStatusDto? Status { get; set; }

    private StorageOverviewDto? Overview { get; set; }

    private string? Error { get; set; }

    private bool Loading { get; set; }

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => LoadAsync(refresh: false);

    private static string UnattachedText(UnattachedStorageDto items) =>
        $"exports {ByteSize.Format(items.ExportsBytes)}, sheet music {ByteSize.Format(items.ScoresBytes)}, generations {ByteSize.Format(items.GenerationsBytes)}, "
        + $"voice profiles {ByteSize.Format(items.VoiceProfilesBytes)}, {CareText.Count(items.LyricsSheets, "lyrics sheet")}.";

    private Task RefreshAsync() => LoadAsync(refresh: true);

    private async Task LoadAsync(bool refresh)
    {
        Loading = true;
        var (overview, error) = await CareCall.TryAsync(() => Client.GetStorageOverviewAsync(refresh));
        Loading = false;
        (Overview, Error) = overview is null ? (Overview, error?.UserMessage) : (overview, null);
    }
}
