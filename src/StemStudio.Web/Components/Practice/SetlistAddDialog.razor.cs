using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Practice;

/// <summary>Track and mix picker of the setlist editor.</summary>
public sealed partial class SetlistAddDialog
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The library's tracks (only completed ones are offered).</summary>
    [Parameter]
    public IReadOnlyList<TrackDto> Tracks { get; set; } = [];

    /// <summary>Raised with the chosen track and mix.</summary>
    [Parameter]
    public EventCallback<(Guid TrackId, Guid? MixId)> OnAdd { get; set; }

    /// <summary>Raised by Cancel and Escape.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private IReadOnlyList<TrackDto> Ready => [.. Tracks.Where(t => t.Status == TrackStatusDto.Completed)];

    private Guid? Picked { get; set; }

    private Guid? Mix { get; set; }

    private IReadOnlyList<MixSummaryDto> Mixes { get; set; } = [];

    private async Task PickAsync(Guid trackId)
    {
        (Picked, Mix) = (trackId, null);
        try
        {
            Mixes = await Api.ListMixesAsync(trackId);
        }
        catch (ApiException)
        {
            Mixes = [];
        }
    }

    private Task ConfirmAsync() => OnAdd.InvokeAsync((Picked!.Value, Mix));
}
