using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Pages;

/// <summary>Import screen (<c>/</c>): URL import, streamed file upload, legal notice (US1, FR-001..FR-005, FR-010).</summary>
public partial class Import
{
    private const string EmptyUrlMessage = "Paste a YouTube video link or a Spotify track link.";
    private const string FallbackContentType = "application/octet-stream";

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>The URL typed by the user.</summary>
    protected string Url { get; set; } = string.Empty;

    /// <summary>The current error, shown in <c>import-error</c>.</summary>
    protected string? Error { get; private set; }

    /// <summary>True while an import request is running.</summary>
    protected bool Busy { get; private set; }

    /// <summary>Submits the URL.</summary>
    /// <returns>A task that completes when the import has finished or failed.</returns>
    protected Task ImportUrlAsync()
    {
        var url = Url.Trim();
        if (url.Length == 0)
        {
            Error = EmptyUrlMessage;
            return Task.CompletedTask;
        }

        return RunImportAsync(() => Api.ImportUrlAsync(url));
    }

    /// <summary>Validates and streams the selected file to the API.</summary>
    /// <param name="args">The file selection.</param>
    /// <returns>A task that completes when the upload has finished or failed.</returns>
    protected Task UploadAsync(InputFileChangeEventArgs args)
    {
        var file = args.File;
        Error = UploadRules.Validate(file.Name, file.Size);
        return Error is null ? RunImportAsync(() => UploadFileAsync(file)) : Task.CompletedTask;
    }

    private async Task<TrackDto> UploadFileAsync(IBrowserFile file)
    {
        await using var stream = file.OpenReadStream(ContractLimits.MaxUploadBytes);
        var contentType = string.IsNullOrEmpty(file.ContentType) ? FallbackContentType : file.ContentType;
        return await Api.ImportUploadAsync(new UploadFile(stream, file.Name, contentType));
    }

    private async Task RunImportAsync(Func<Task<TrackDto>> import)
    {
        Busy = true;
        Error = null;
        try
        {
            var track = await import();
            Navigation.NavigateTo($"tracks/{track.Id}");
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
        finally
        {
            Busy = false;
        }
    }
}
