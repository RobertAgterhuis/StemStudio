using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Api;
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

    private IReadOnlyList<MixSummaryDto>? Items { get; set; }

    private string? Error { get; set; }

    private MixSummaryDto? Renaming { get; set; }

    private string? RenameError { get; set; }

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => RunAsync(() => Task.CompletedTask);

    private static string Iso(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string Local(DateTimeOffset time) => time.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

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
