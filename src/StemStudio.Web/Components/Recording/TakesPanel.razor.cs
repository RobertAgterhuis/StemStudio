using Microsoft.AspNetCore.Components;
using StemStudio.Web.Experience;
using StemStudio.Web.Recording;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Recording;

/// <summary>Code-behind of the Takes panel: selection, stars and "Delete the other passes" with confirmation.</summary>
public sealed partial class TakesPanel
{
    [Inject]
    private IConfirmService Confirm { get; set; } = default!;

    /// <summary>The cascaded level (sessions are grouped from Advanced, R10).</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    /// <summary>The takes.</summary>
    [Parameter, EditorRequired]
    public TakeLibrary Takes { get; set; } = default!;

    /// <summary>The track (batch delete).</summary>
    [Parameter]
    public Guid TrackId { get; set; }

    /// <summary>Extra content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Raised after a change the mixer shows (strip, stars, deletions).</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    private bool ShowsGroup(TakeSession session) =>
        session.IsLoop && (Experience ?? ExperienceContext.Default).IsVisible("recording.loop");

    private bool IsLoaded(TakeItem item) => ReferenceEquals(item, Takes.Loaded);

    private Task SelectAsync(TakeItem item) => ThenChangedAsync(() => Takes.LoadIntoStripAsync(item));

    private Task StarAsync(TakeItem item) => ThenChangedAsync(() => Takes.ToggleStarAsync(item));

    private async Task KeepStarredAsync(TakeSession session)
    {
        var count = session.Unstarred.Count;
        var message = $"Delete the {count} {(count == 1 ? "pass" : "passes")} without a star? Their files are removed from this computer.";
        if (await Confirm.ConfirmAsync("Delete the other passes", message, "Delete"))
        {
            await ThenChangedAsync(() => Takes.KeepStarredAsync(TrackId, session));
        }
    }

    private async Task ThenChangedAsync(Func<Task> action)
    {
        await action();
        await OnChanged.InvokeAsync();
    }
}
