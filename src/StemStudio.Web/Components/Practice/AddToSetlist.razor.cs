using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Setlists;
using StemStudio.Web.Api;
using StemStudio.Web.Practice;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Practice;

/// <summary>Adds a track (with an optional mix) to the end of a chosen setlist.</summary>
public sealed partial class AddToSetlist
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    /// <summary>The track to add.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>The mix to add with it, or null.</summary>
    [Parameter]
    public Guid? MixId { get; set; }

    /// <summary>Button text.</summary>
    [Parameter]
    public string Text { get; set; } = "Add to setlist";

    private string TestId => MixId is { } mix ? $"add-to-setlist-mix-{mix}" : $"add-to-setlist-{TrackId}";

    private bool Open { get; set; }

    private IReadOnlyList<SetlistSummaryDto>? Choices { get; set; }

    private async Task ToggleAsync()
    {
        Open = !Open;
        if (Open)
        {
            Choices = null;
            Choices = await Api.ListSetlistsAsync();
        }
    }

    private async Task AddAsync(SetlistSummaryDto setlist)
    {
        Open = false;
        try
        {
            var current = await Api.GetSetlistAsync(setlist.Id);
            var entries = SetlistEntries.Add(current.Entries, TrackId, MixId);
            if (entries is null)
            {
                Toasts.Show($"{setlist.Name} is full ({SetlistEntries.MaxEntries} songs).");
                return;
            }

            await Api.ReplaceSetlistEntriesAsync(setlist.Id, new SetlistEntriesUpdateRequest { Entries = entries });
            Toasts.Show($"Added to {setlist.Name}");
        }
        catch (ApiException ex)
        {
            Toasts.Show(ex.Error.UserMessage);
        }
    }
}
