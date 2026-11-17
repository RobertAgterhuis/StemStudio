using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.LibraryCare;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Care;

/// <summary>
/// Loads the archive list and the last library job, follows the active job through the status the page passes in
/// (when it disappears, the final job and the list are read again) and runs the list actions.
/// </summary>
public sealed partial class BackupsTab
{
    private Guid? _followed;

    [Inject]
    private ILibraryCareClient Client { get; set; } = default!;

    [Inject]
    private LibraryCareStatusSource StatusSource { get; set; } = default!;

    /// <summary>The library care status (null before the first answer).</summary>
    [Parameter]
    public LibraryCareStatusDto? Status { get; set; }

    private BackupListDto? List { get; set; }

    private string? ListError { get; set; }

    private LibraryJobDto? LastJob { get; set; }

    private LibraryJobDto? ShownJob => Status?.ActiveJob ?? LastJob;

    private bool JobRunning => Status?.ActiveJob is not null;

    private FolderStateDto? Folder => List?.Folder ?? Status?.BackupFolder;

    private string? NewBlocked => JobRunning ? "A library job is running; start the backup when it has finished."
        : Folder is { } folder && FolderProblem(folder) is not null ? "The backups folder is not available."
        : null;

    private bool BackupDialogOpen { get; set; }

    private BackupSummaryDto? RestoreFor { get; set; }

    private BackupSummaryDto? DeleteFor { get; set; }

    private string? DetailsFor { get; set; }

    private string? ActionError { get; set; }

    private string? ActionErrorCode { get; set; }

    private string? CancelError { get; set; }

    private string? CancelErrorCode { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadListAsync();
        if (Status?.ActiveJob is null)
        {
            var (jobs, _) = await ApiCall.TryAsync(() => Client.ListJobsAsync(1));
            LastJob = jobs is [var last, ..] ? last : null;
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        var active = Status?.ActiveJob;
        if (_followed is { } followed && active?.JobId != followed)
        {
            _followed = null;
            await JobEndedAsync(followed);
        }

        Follow(active);
    }

    private void Follow(LibraryJobDto? active)
    {
        if (active is not null)
        {
            (_followed, LastJob) = (active.JobId, active);
        }
    }

    private static string Flag(bool value) => value ? "true" : "false";

    private static string FolderText(FolderStateDto folder)
    {
        var where = folder.HostPathHint ?? "the backups folder";
        return folder.FreeBytes is { } free
            ? $"Backups are written to {where}: {ByteSize.Format(free)} free."
            : $"Backups are written to {where}.";
    }

    private static string? FolderProblem(FolderStateDto folder) =>
        !folder.Available ? $"The backups folder is not available: {folder.Reason ?? "it does not exist"}. Create it and make it writable, then reload the page."
        : !folder.Writable ? $"The backups folder is read-only: {folder.Reason ?? "StemStudio may not write to it"}. Make it writable, then reload the page."
        : null;

    private async Task LoadListAsync()
    {
        var (list, error) = await ApiCall.TryAsync(() => Client.ListBackupsAsync());
        (List, ListError) = list is null ? (List, error) : (list, null);
    }

    private async Task JobEndedAsync(Guid jobId)
    {
        var (job, _) = await ApiCall.TryAsync(() => Client.GetJobAsync(jobId));
        LastJob = job ?? LastJob;
        await LoadListAsync();
    }

    private async Task FollowJobAsync(LibraryJobDto job)
    {
        (_followed, LastJob) = (job.JobId, job);
        (CancelError, CancelErrorCode) = (null, null);
        await StatusSource.RefreshAsync();
    }

    private void OpenBackupDialog() => BackupDialogOpen = true;

    private async Task OnBackupStartedAsync(LibraryJobDto job)
    {
        BackupDialogOpen = false;
        await FollowJobAsync(job);
    }

    private async Task OnRestoreStartedAsync(LibraryJobDto job)
    {
        RestoreFor = null;
        await FollowJobAsync(job);
    }

    private async Task RefreshAllAsync()
    {
        await StatusSource.RefreshAsync();
        await LoadListAsync();
    }

    private async Task CancelJobAsync()
    {
        var jobId = ShownJob!.JobId;
        var (job, error) = await CareCall.TryAsync(() => Client.CancelJobAsync(jobId));
        (CancelError, CancelErrorCode) = (error?.UserMessage, error?.Code);
        if (job is not null)
        {
            LastJob = job;
            await StatusSource.RefreshAsync();
        }
    }

    private void ShowDetails(BackupSummaryDto archive) => DetailsFor = archive.FileName;

    private void OpenRestore(BackupSummaryDto archive) => RestoreFor = archive;

    private void AskDelete(BackupSummaryDto archive) => DeleteFor = archive;

    private Task VerifyAsync(BackupSummaryDto archive) => RunActionAsync(async () => await FollowJobAsync(await Client.VerifyBackupAsync(archive.FileName)));

    private async Task DeleteAsync()
    {
        var archive = DeleteFor!;
        DeleteFor = null;
        await RunActionAsync(async () =>
        {
            await Client.DeleteBackupAsync(archive.FileName);
            if (DetailsFor == archive.FileName)
            {
                DetailsFor = null;
            }

            await LoadListAsync();
        });
    }

    private async Task RunActionAsync(Func<Task> action)
    {
        try
        {
            (ActionError, ActionErrorCode) = (null, null);
            await action();
        }
        catch (ApiException ex)
        {
            (ActionError, ActionErrorCode) = (ex.Error.UserMessage, ex.Error.Code);
        }
    }
}
