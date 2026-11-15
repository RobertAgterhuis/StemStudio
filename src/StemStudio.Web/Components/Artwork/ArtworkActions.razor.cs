using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Artwork;

/// <summary>Upload and reset of the user's own picture; reports the updated track to the page.</summary>
public sealed partial class ArtworkActions
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The track whose picture is changed.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>Raised with the track after a successful upload or reset.</summary>
    [Parameter]
    public EventCallback<TrackDto> OnChanged { get; set; }

    private bool Busy { get; set; }

    private string? Error { get; set; }

    private Task UploadAsync(InputFileChangeEventArgs args)
    {
        var file = args.File;
        Error = ArtworkRules.Validate(file.ContentType, file.Size);
        return Error is null ? RunAsync(() => SendAsync(file)) : Task.CompletedTask;
    }

    private async Task<TrackDto> SendAsync(IBrowserFile file)
    {
        await using var stream = file.OpenReadStream(ContractLimits.MaxArtworkUploadBytes);
        return await Api.SetArtworkAsync(Track.Id, new UploadFile(stream, file.Name, file.ContentType));
    }

    private Task ResetAsync() => RunAsync(() => Api.ResetArtworkAsync(Track.Id));

    private async Task RunAsync(Func<Task<TrackDto>> change)
    {
        Busy = true;
        var track = await TryAsync(change);
        Busy = false;
        if (track is not null)
        {
            await OnChanged.InvokeAsync(track);
        }
    }

    private async Task<TrackDto?> TryAsync(Func<Task<TrackDto>> change)
    {
        try
        {
            return await change();
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
            return null;
        }
    }
}
