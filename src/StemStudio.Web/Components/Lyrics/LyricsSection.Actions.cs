using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;
using StemStudio.Web.Lyrics;

namespace StemStudio.Web.Components.Lyrics;

/// <summary>Align, cancel and "enter lyrics yourself" of the lyrics card (feature 006, US2).</summary>
public sealed partial class LyricsSection
{
    private bool PasteOpen { get; set; }

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
