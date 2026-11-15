using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;
using StemStudio.Web.Lyrics;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Lyrics;

/// <summary>Align, cancel, "enter lyrics yourself", search again and remove of the lyrics card (feature 006, US2, US3).</summary>
public sealed partial class LyricsSection
{
    private const string RemoveMessage =
        "Remove the lyrics of this song? StemStudio will not search for them again by itself; you can search again or enter them yourself later.";

    [Inject]
    private IConfirmService Confirm { get; set; } = default!;

    private bool PasteOpen { get; set; }

    private bool CanSearch => Lyrics!.Status is LyricsStatusDto.Removed or LyricsStatusDto.NotFound or LyricsStatusDto.Failed
        or LyricsStatusDto.Disabled or LyricsStatusDto.Found;

    private bool CanRemove => Lyrics!.Status is not (LyricsStatusDto.None or LyricsStatusDto.Removed) && !IsBusyState;

    private Task SearchAsync() => RunAsync(ct => Api.SearchLyricsAsync(Track.Id, ct));

    private async Task RemoveAsync()
    {
        if (await Confirm.ConfirmAsync("Remove lyrics", RemoveMessage, "Remove"))
        {
            await RunAsync(ct => Api.RemoveLyricsAsync(Track.Id, ct));
        }
    }

    private Task AlignAsync() => RunAsync(ct => Api.AlignLyricsAsync(Track.Id, ct));

    private Task CancelAlignAsync() => RunAsync(ct => Api.CancelLyricsAlignmentAsync(Track.Id, ct));

    private void OpenPaste() => PasteOpen = true;

    private void ClosePaste() => PasteOpen = false;

    /// <summary>Checks and stores pasted text; the form stays open with the reason when it is refused.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The reason it was refused, or null when it was stored.</returns>
    private Task<string?> PasteAsync(string text) =>
        LyricsPaste.Validate(text) is { } problem ? Task.FromResult<string?>(problem) : StoreAsync(text);

    private async Task<string?> StoreAsync(string text)
    {
        try
        {
            Apply(await Api.SetLyricsAsync(Track.Id, new SetLyricsRequest(text), Stopping));
            PasteOpen = false;
            StateHasChanged();
            return null;
        }
        catch (ApiException ex)
        {
            return ex.Error.UserMessage;
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task<LyricsDto>> action)
    {
        Busy = true;
        try
        {
            Apply(await action(Stopping));
        }
        catch (ApiException ex)
        {
            ActionError = ex.Error.UserMessage;
        }

        Busy = false;
    }

    private void Apply(LyricsDto lyrics)
    {
        (Lyrics, ActionError) = (lyrics, null);
        StartPolling();
    }
}
