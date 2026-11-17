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
    private int? _dragging;

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

    private async Task SaveAsync(Func<Task<SetlistDto>> call)
    {
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

    private Task EntriesAsync(List<SetlistEntryInputDto>? entries) => entries is null
        ? Task.CompletedTask
        : SaveAsync(() => Api.ReplaceSetlistEntriesAsync(SetlistId, new SetlistEntriesUpdateRequest { Entries = entries }));

    private Task RenameAsync(ChangeEventArgs args) => args.Value?.ToString()?.Trim() is { Length: > 0 } name
        ? SaveAsync(() => Api.UpdateSetlistAsync(SetlistId, new SetlistUpdateRequest { Name = name }))
        : Task.CompletedTask;

    private Task NoteAsync(ChangeEventArgs args) =>
        SaveAsync(() => Api.UpdateSetlistAsync(SetlistId, new SetlistUpdateRequest { Note = args.Value?.ToString() ?? string.Empty }));

    private Task AcknowledgeRemovedAsync() => SaveAsync(() => Api.GetSetlistAsync(SetlistId, acknowledgeRemoved: true));

    private Task MoveAsync(int from, int to) => EntriesAsync(SetlistEntries.Move(Setlist!.Entries, from, to));

    private Task DropAsync(int to)
    {
        var from = _dragging;
        _dragging = null;
        return from is { } index ? MoveAsync(index, to) : Task.CompletedTask;
    }

    private Task RemoveAsync(SetlistEntryDto entry) => EntriesAsync(SetlistEntries.Remove(Setlist!.Entries, entry.Id));

    private Task SetNoteAsync(SetlistEntryDto entry, object? value) =>
        EntriesAsync(SetlistEntries.Change(Setlist!.Entries, entry.Id, e => e with { Note = value?.ToString() ?? string.Empty }));

    private Task SetMixAsync(SetlistEntryDto entry, object? value) => EntriesAsync(SetlistEntries.Change(Setlist!.Entries, entry.Id,
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
        await EntriesAsync(SetlistEntries.Add(Setlist!.Entries, choice.TrackId, choice.MixId));
    }

    private void Play() => Navigation.NavigateTo(SetlistEntries.PlayRoute(SetlistId, 1));
}
