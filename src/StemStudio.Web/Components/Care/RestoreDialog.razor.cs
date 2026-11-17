using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.LibraryCare;

namespace StemStudio.Web.Components.Care;

/// <summary>Loads the archive details and the library's free space, then starts the restore (013 US2).</summary>
public sealed partial class RestoreDialog
{
    [Inject]
    private ILibraryCareClient Client { get; set; } = default!;

    /// <summary>The archive to restore.</summary>
    [Parameter, EditorRequired]
    public BackupSummaryDto Archive { get; set; } = default!;

    /// <summary>The library care status when the dialog opened (empty library, active jobs, previous library).</summary>
    [Parameter, EditorRequired]
    public LibraryCareStatusDto Status { get; set; } = default!;

    /// <summary>The restore was accepted; the host closes the dialog and follows the job.</summary>
    [Parameter]
    public EventCallback<LibraryJobDto> OnStarted { get; set; }

    /// <summary>Cancel or Escape.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private RestoreForm Form { get; set; } = default!;

    private string? Error { get; set; }

    private string? ErrorCode { get; set; }

    private bool Busy { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Form = new RestoreForm(Status);
        await base.OnInitializedAsync();
        var (details, error) = await CareCall.TryAsync(() => Client.GetBackupAsync(Archive.FileName));
        Form.Details = details;
        ShowError(error);
        var (overview, _) = await CareCall.TryAsync(() => Client.GetStorageOverviewAsync());
        Form.FreeBytes = overview?.Volumes.FirstOrDefault(v => v.Id == StorageVolumeIdDto.Library)?.FreeBytes;
    }

    private Task OnKeyAsync(KeyboardEventArgs args) => CloseOnEscapeAsync(args, OnClose);

    private void ShowError(ApiError? error) => (Error, ErrorCode) = (error?.UserMessage, error?.Code);

    private async Task StartAsync()
    {
        Busy = true;
        ShowError(null);
        var options = Form.Options;
        var (job, error) = await CareCall.TryAsync(() => Client.RestoreBackupAsync(Archive.FileName, options));
        Busy = false;
        ShowError(error);
        Form.TakeBlockers(error);
        if (job is not null)
        {
            await OnStarted.InvokeAsync(job);
        }
    }
}
