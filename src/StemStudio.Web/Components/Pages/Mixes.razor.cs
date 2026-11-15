using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.Mixer;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Pages;

/// <summary>Mix library (<c>/mixes</c>): list, load, duplicate, rename and delete saved mixes (US4, FR-017).</summary>
public sealed partial class Mixes
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IConfirmService Confirm { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>The cascaded level (Advanced without a shell).</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    private IReadOnlyList<MixSummaryDto>? Items { get; set; }

    private Dictionary<Guid, IReadOnlyList<string>> TrackStems { get; set; } = [];

    private string Search { get; set; } = string.Empty;

    private IReadOnlyList<MixSummaryDto> Visible => (Items ?? []).Where(Matches).ToList();

    /// <summary>Expert: compact rows with icon buttons (names stay in aria-label, labels in a visible tooltip).</summary>
    private bool IconOnly => (Experience ?? ExperienceContext.Default).Is(ExperienceLevel.Expert);

    private string? IconClass => IconOnly ? "ss-icon-only" : null;

    private string? Error { get; set; }

    private MixSummaryDto? Renaming { get; set; }

    private string? RenameError { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await RunAsync(() => Task.CompletedTask);
        TrackStems = await LoadTrackStemsAsync();
    }

    private static string Iso(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string Local(DateTimeOffset time) => time.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadTrackStemsAsync()
    {
        try
        {
            var tracks = await Api.ListTracksAsync();
            return tracks.ToDictionary(t => t.Id, t => (IReadOnlyList<string>)[.. t.Stems.Select(s => s.Name)]);
        }
        catch (ApiException)
        {
            return [];
        }
    }

    private IReadOnlyList<string> StemsOf(MixSummaryDto mix) => TrackStems.GetValueOrDefault(mix.TrackId) ?? [];

    private bool Matches(MixSummaryDto mix) =>
        Search.Trim().Length == 0
        || mix.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)
        || mix.TrackTitle.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase);

    private void OnSearch(ChangeEventArgs args) => Search = args.Value?.ToString() ?? string.Empty;

    private string? Label(string text) => IconOnly ? null : text;

    private string? Tooltip(string text) => IconOnly ? text : null;

    private void Load(MixSummaryDto mix) => Navigation.NavigateTo($"tracks/{mix.TrackId}/mixer?mixId={mix.Id}");

    private Task DuplicateAsync(MixSummaryDto mix) => RunAsync(() => Api.DuplicateMixAsync(mix.Id));

    private async Task DeleteAsync(MixSummaryDto mix)
    {
        var message = $"Delete the mix \"{mix.Name}\"? The track and its stems remain.";
        if (await Confirm.ConfirmAsync("Delete mix", message, "Delete"))
        {
            await RunAsync(() => Api.DeleteMixAsync(mix.Id));
        }
    }

    private void OpenRename(MixSummaryDto mix)
    {
        Renaming = mix;
        RenameError = null;
    }

    private void CloseRename() => Renaming = null;

    private async Task RenameAsync(string name)
    {
        RenameError = MixNames.Validate(name);
        if (RenameError is not null)
        {
            return;
        }

        try
        {
            var mix = await Api.GetMixAsync(Renaming!.Id);
            await Api.UpdateMixAsync(mix.Id, mix.Settings with { Name = name.Trim() });
            Renaming = null;
            await RunAsync(() => Task.CompletedTask);
        }
        catch (ApiException ex)
        {
            RenameError = MixNames.SaveError(ex.Error, name.Trim());
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            Items = await Api.ListMixesAsync();
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }
}
