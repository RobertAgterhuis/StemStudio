using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.LibraryCare;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Care;

/// <summary>Estimates and starts a backup (013 US1); the estimate is read again after every change of the options.</summary>
public sealed partial class BackupDialog
{
    private int _estimateVersion;

    [Inject]
    private ILibraryCareClient Client { get; set; } = default!;

    /// <summary>The backup was accepted; the host closes the dialog and follows the job.</summary>
    [Parameter]
    public EventCallback<LibraryJobDto> OnStarted { get; set; }

    /// <summary>Cancel or Escape.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private BackupForm Form { get; } = new();

    private BackupEstimateDto? Estimate { get; set; }

    private string? EstimateError { get; set; }

    private string? Error { get; set; }

    private string? ErrorCode { get; set; }

    private bool Busy { get; set; }

    private bool CanStart => Estimate is { Enough: true } && !Busy;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await EstimateAsync();
    }

    private static string Flag(bool value) => value ? "true" : "false";

    private static string EstimateText(BackupEstimateDto estimate)
    {
        var free = estimate.Folder.FreeBytes is { } bytes ? $"{ByteSize.Format(bytes)} free in the backups folder" : "free space unknown";
        var verdict = estimate.Enough ? "enough space" : "not enough space";
        return $"About {ByteSize.Format(estimate.TotalBytes)}; {free} ({verdict}).";
    }

    private Task OnKeyAsync(KeyboardEventArgs args) => CloseOnEscapeAsync(args, OnClose);

    private Task ChooseAsync(BackupProfile profile)
    {
        Form.Choose(profile);
        return EstimateAsync();
    }

    private Task SetAsync(BackupCategoryDto category, bool included)
    {
        Form.Set(category, included);
        return EstimateAsync();
    }

    private async Task EstimateAsync()
    {
        var version = ++_estimateVersion;
        var options = Form.Options;
        var (estimate, error) = await CareCall.TryAsync(() => Client.EstimateBackupAsync(options));
        if (version != _estimateVersion)
        {
            return; // An answer for options that have changed since.
        }

        (Estimate, EstimateError) = (estimate, error?.UserMessage);
        (Error, ErrorCode) = (error?.UserMessage, error?.Code);
    }

    private async Task StartAsync()
    {
        Busy = true;
        (Error, ErrorCode) = (null, null);
        var (job, error) = await CareCall.TryAsync(() => Client.CreateBackupAsync(Form.Options));
        Busy = false;
        (Error, ErrorCode) = (error?.UserMessage, error?.Code);
        if (job is not null)
        {
            await OnStarted.InvokeAsync(job);
        }
    }
}
