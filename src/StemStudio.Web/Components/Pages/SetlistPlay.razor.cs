using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Setlists;
using StemStudio.Web.Api;
using StemStudio.Web.Interop;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Components.Pages;

/// <summary>Setlist play mode: loads the setlist, registers the <c>setlist</c> key map (next/previous song) and moves between entries.</summary>
public sealed partial class SetlistPlay
{
    private IAsyncDisposable? _keys;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ShortcutKeys Keys { get; set; } = default!;

    /// <summary>The setlist.</summary>
    [Parameter]
    public Guid SetlistId { get; set; }

    /// <summary>1-based position from the route.</summary>
    [Parameter]
    public int Position { get; set; }

    private SetlistDto? Setlist { get; set; }

    private string? Error { get; set; }

    private SetlistEntryDto? Entry => Setlist?.Entries.ElementAtOrDefault(Position - 1);

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await (_keys?.DisposeAsync() ?? ValueTask.CompletedTask);

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        try
        {
            Setlist = Setlist?.Id == SetlistId ? Setlist : await Api.GetSetlistAsync(SetlistId);
            Error = Entry is null ? "This setlist has no song at that position." : null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _keys = await Keys.RegisterAsync(ShortcutScope.Setlist, id => InvokeAsync(() => OnCommand(id)));
        }
    }

    private void OnCommand(string id)
    {
        switch (id)
        {
            case "setlist.next":
                Go(Position + 1);
                break;
            case "setlist.previous":
                Go(Position - 1);
                break;
        }
    }

    private void Go(int position)
    {
        if (Setlist is not null && position >= 1 && position <= Setlist.Entries.Count)
        {
            Navigation.NavigateTo(SetlistEntries.PlayRoute(SetlistId, position));
        }
    }

    private void Exit() => Navigation.NavigateTo($"setlists/{SetlistId}");
}
