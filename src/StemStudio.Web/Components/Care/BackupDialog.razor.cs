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
        var (estimate, error) = await TryEstimateAsync(Form.Options);
        if (version != _estimateVersion)
        {
            return; // An answer for options that have changed since.
        }

        (Estimate, EstimateError) = (estimate, error?.UserMessage);
        (Error, ErrorCode) = (error?.UserMessage, error?.Code);
    }

    private async Task<(BackupEstimateDto? Estimate, ApiError? Error)> TryEstimateAsync(BackupOptionsDto options)
    {
        try
        {
            return (await Client.EstimateBackupAsync(options), null);
        }
        catch (ApiException ex)
        {
            return (null, ex.Error);
        }
    }

    private void ClearError() => (Error, ErrorCode) = (null, null);

    private async Task StartAsync()
    {
        Busy = true;
        ClearError();
        try
        {
            var job = await Client.CreateBackupAsync(Form.Options);
            await OnStarted.InvokeAsync(job);
        }
        catch (ApiException ex)
        {
            (Error, ErrorCode) = (ex.Error.UserMessage, ex.Error.Code);
        }
        finally
        {
            Busy = false;
        }
    }
}
