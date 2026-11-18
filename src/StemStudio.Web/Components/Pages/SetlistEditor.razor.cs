using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Setlists;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Components.Pages;

/// <summary>
/// Setlist editor: every change is saved at once (<c>updateSetlist</c> for name and note, <c>replaceSetlistEntries</c> for
/// the songs); a failed save shows <c>setlist-save-error</c> and reloads the stored setlist.
/// </summary>
public sealed partial class SetlistEditor
{
    private readonly Dictionary<Guid, IReadOnlyList<MixSummaryDto>> _mixes = [];
    private readonly Dictionary<Guid, Radzen.Blazor.RadzenButton> _upButtons = [];
    private readonly Dictionary<Guid, Radzen.Blazor.RadzenButton> _downButtons = [];
    private int? _dragging;
    private Task _saving = Task.CompletedTask;
    private (Guid Entry, bool Down)? _focusAfterMove;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IOptions<StemStudioWebOptions> Options { get; set; } = default!;

    /// <summary>The setlist from the route.</summary>
    [Parameter]
    public Guid SetlistId { get; set; }

    /// <summary>The cascaded level.</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    private SetlistDto? Setlist { get; set; }

    private string? Error { get; set; }

    private bool Adding { get; set; }

    private bool PdfOpen { get; set; }

    private IReadOnlyList<TrackDto> Tracks { get; set; } = [];

    private bool PdfOptions => (Experience ?? ExperienceContext.Default).IsVisible("setlists.pdf-options");

    private string PdfUrl => new Uri(Options.Value.PublicApiBaseUrl, ApiRoutes.SetlistPdf(SetlistId, SetlistPdfOptions.Default)).ToString();

    /// <inheritdoc />
    protected override Task OnParametersSetAsync() => SaveAsync(() => Api.GetSetlistAsync(SetlistId));

    private IReadOnlyList<MixSummaryDto> MixesOf(SetlistEntryDto entry) =>
        _mixes.TryGetValue(entry.TrackId, out var mixes)
            ? mixes
            : entry.MixId is { } id ? [new MixSummaryDto(id, entry.TrackId, entry.TrackTitle, entry.MixName ?? "Saved mix", DateTimeOffset.MinValue)] : [];

    // Saves run one after the other: a change made while another is saving (a note, then Move at once) is built from the
    // setlist that save returns, so it never sends entries without the change before it.
    private Task SaveAsync(Func<Task<SetlistDto>> call)
    {
        _saving = AfterAsync(_saving, call);
        return _saving;
    }

    private async Task AfterAsync(Task previous, Func<Task<SetlistDto>> call)
    {
        await previous.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        try
        {
            Setlist = await call();
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
            await ReloadAfterErrorAsync();
        }
    }

    private async Task ReloadAfterErrorAsync()
    {
        try
        {
            Setlist = Setlist is null ? null : await Api.GetSetlistAsync(SetlistId);
        }
        catch (ApiException)
        {
            // The error above is already shown.
        }
    }

    // The change is applied to the entries when its turn comes (see SaveAsync); null means no change, and nothing is sent.
    private Task EntriesAsync(Func<IReadOnlyList<SetlistEntryDto>, List<SetlistEntryInputDto>?> change) =>
        SaveAsync(() => change(Setlist!.Entries) is { } entries
            ? Api.ReplaceSetlistEntriesAsync(SetlistId, new SetlistEntriesUpdateRequest { Entries = entries })
            : Task.FromResult(Setlist!));

    private Task RenameAsync(ChangeEventArgs args) => args.Value?.ToString()?.Trim() is { Length: > 0 } name
        ? SaveAsync(() => Api.UpdateSetlistAsync(SetlistId, new SetlistUpdateRequest { Name = name }))
        : Task.CompletedTask;

    private Task NoteAsync(ChangeEventArgs args) =>
        SaveAsync(() => Api.UpdateSetlistAsync(SetlistId, new SetlistUpdateRequest { Note = args.Value?.ToString() ?? string.Empty }));

    private Task AcknowledgeRemovedAsync() => SaveAsync(() => Api.GetSetlistAsync(SetlistId, acknowledgeRemoved: true));

    private Task MoveAsync(int from, int to) => EntriesAsync(entries => SetlistEntries.Move(entries, from, to));

    // Keyboard reorder (US5 scenario 2): the focus follows the moved song to its new place.
    // The place is looked up when the move runs, so a move queued behind another save still moves this song by one.
    private Task MoveByButtonAsync(SetlistEntryDto entry, int from, int to)
    {
        _focusAfterMove = (entry.Id, to > from);
        return EntriesAsync(entries => entries.ToList().FindIndex(e => e.Id == entry.Id) is var at and >= 0
            ? SetlistEntries.Move(entries, at, at + (to - from))
            : null);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusAfterMove is not { } move || Setlist is null)
        {
            return;
        }

        _focusAfterMove = null;
        var index = Setlist.Entries.ToList().FindIndex(e => e.Id == move.Entry);
        // The button that was used, unless it is disabled at the new place (first or last): then the other one.
        var down = move.Down ? index < Setlist.Entries.Count - 1 : index == 0;
        if (index >= 0 && (down ? _downButtons : _upButtons).TryGetValue(move.Entry, out var button))
        {
            await button.Element.FocusAsync();
        }
    }

    private Task DropAsync(int to)
    {
        var from = _dragging;
        _dragging = null;
        return from is { } index ? MoveAsync(index, to) : Task.CompletedTask;
    }

    private Task RemoveAsync(SetlistEntryDto entry) => EntriesAsync(entries => SetlistEntries.Remove(entries, entry.Id));

    private Task SetNoteAsync(SetlistEntryDto entry, object? value) =>
        EntriesAsync(entries => SetlistEntries.Change(entries, entry.Id, e => e with { Note = value?.ToString() ?? string.Empty }));

    private Task SetMixAsync(SetlistEntryDto entry, object? value) => EntriesAsync(entries => SetlistEntries.Change(entries, entry.Id,
        e => e with { MixId = Guid.TryParse(value?.ToString(), out var id) ? id : null }));

    private async Task LoadMixesAsync(Guid trackId)
    {
        if (!_mixes.ContainsKey(trackId))
        {
            _mixes[trackId] = await Api.ListMixesAsync(trackId);
        }
    }

    private async Task OpenAddAsync()
    {
        Tracks = await Api.ListTracksAsync();
        Adding = true;
    }

    private async Task AddAsync((Guid TrackId, Guid? MixId) choice)
    {
        Adding = false;
        await EntriesAsync(entries => SetlistEntries.Add(entries, choice.TrackId, choice.MixId));
    }

    private void Play() => Navigation.NavigateTo(SetlistEntries.PlayRoute(SetlistId, 1));
}
