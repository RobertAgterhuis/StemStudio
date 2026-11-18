using Microsoft.AspNetCore.Components;
using StemStudio.Web.Experience;
using StemStudio.Web.Recording;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Recording;

/// <summary>Code-behind of the Takes panel: selection, stars, play from start, rename, delete, retry and keep-starred.</summary>
public sealed partial class TakesPanel
{
    private TakeItem? _editing;
    private string? _renameError;

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

    /// <summary>Moves the playhead (play from the take start).</summary>
    [Parameter]
    public EventCallback<double> OnSeek { get; set; }

    /// <summary>A take's export was requested (US6).</summary>
    [Parameter]
    public EventCallback<TakeItem> OnExport { get; set; }

    /// <summary>Extra content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Raised after a change the mixer shows (strip, stars, deletions, a retried take to poll).</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    private bool ShowsGroup(TakeSession session) =>
        session.IsLoop && (Experience ?? ExperienceContext.Default).IsVisible("recording.loop");

    private bool IsLoaded(TakeItem item) => ReferenceEquals(item, Takes.Loaded);

    private bool IsEditing(TakeItem item) => ReferenceEquals(item, _editing);

    private Task SelectAsync(TakeItem item) => ThenChangedAsync(() => Takes.LoadIntoStripAsync(item));

    private Task StarAsync(TakeItem item) => ThenChangedAsync(() => Takes.ToggleStarAsync(item));

    private Task RetryAsync(TakeItem item) => ThenChangedAsync(() => Takes.RetryAsync(item));

    private async Task PlayFromAsync(TakeItem item)
    {
        await (IsLoaded(item) ? Task.CompletedTask : Takes.LoadIntoStripAsync(item));
        await OnSeek.InvokeAsync(item.StartSample / (double)StemStudio.Contracts.Recording.RecordingLimits.SampleRate);
        await OnChanged.InvokeAsync();
    }

    private void StartRename(TakeItem item) => (_editing, _renameError) = (item, null);

    private void CancelRename() => (_editing, _renameError) = (null, null);

    private async Task RenameAsync(TakeItem item, string name)
    {
        _renameError = await Takes.RenameAsync(item, name);
        _editing = _renameError is null ? null : item;
        await OnChanged.InvokeAsync();
    }

    private async Task DeleteAsync(TakeItem item)
    {
        if (await Confirm.ConfirmAsync("Delete take", $"Delete {item.Name}? Its audio is removed from this computer.", "Delete"))
        {
            await ThenChangedAsync(() => Takes.DeleteAsync(item));
        }
    }

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
