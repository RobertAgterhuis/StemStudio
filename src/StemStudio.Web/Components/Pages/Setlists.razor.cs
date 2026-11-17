using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Setlists;
using StemStudio.Web.Api;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Components.Pages;

/// <summary>Setlists page: list, create (then open the editor), play and delete with confirmation.</summary>
public sealed partial class Setlists
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private IReadOnlyList<SetlistSummaryDto>? Items { get; set; }

    private string? Error { get; set; }

    private bool Creating { get; set; }

    private string NewName { get; set; } = string.Empty;

    private SetlistSummaryDto? Deleting { get; set; }

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            Items = await Api.ListSetlistsAsync();
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    private async Task CreateAsync()
    {
        try
        {
            var created = await Api.CreateSetlistAsync(new SetlistCreateRequest { Name = NewName.Trim() });
            Navigation.NavigateTo($"setlists/{created.Id}");
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    private void Play(SetlistSummaryDto setlist) => Navigation.NavigateTo(SetlistEntries.PlayRoute(setlist.Id, 1));

    private async Task DeleteAsync()
    {
        var setlist = Deleting!;
        Deleting = null;
        try
        {
            await Api.DeleteSetlistAsync(setlist.Id);
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }

        await LoadAsync();
    }
}
