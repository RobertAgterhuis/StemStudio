using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Pages;

/// <summary>Retry and delete actions of the track detail page.</summary>
public sealed partial class TrackDetail
{
    /// <summary>Route of the track list, the destination after a successful delete.</summary>
    internal const string TrackListRoute = "/tracks";

    private bool _retriedThisSession;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>True while the retry request is running (busy button, no double submit).</summary>
    private bool IsRetrying { get; set; }

    /// <summary>True while the inline delete confirmation is shown.</summary>
    private bool ConfirmingDelete { get; set; }

    /// <summary>True while the delete request is running.</summary>
    private bool IsDeleting { get; set; }

    /// <summary>True when a retry started on this page ended in <c>failed</c> again.</summary>
    private bool RetryFailedAgain => _retriedThisSession && !IsRetrying && Track!.Status == TrackStatusDto.Failed;

    /// <summary>Only terminal tracks can be deleted (409 otherwise).</summary>
    private bool CanDelete => Track!.Status is TrackStatusDto.Failed or TrackStatusDto.Completed;

    private async Task RetryAsync()
    {
        IsRetrying = true;
        _retriedThisSession = false;
        try
        {
            await SubmitRetryAsync();
        }
        finally
        {
            IsRetrying = false;
        }
    }

    private async Task SubmitRetryAsync()
    {
        try
        {
            Track = await Api.RetryTrackAsync(Id, Stopping);
            ActionError = null;
            _retriedThisSession = true;
            StartPolling();
        }
        catch (ApiException ex)
        {
            ActionError = ex.Error.UserMessage;
        }
    }

    private void AskDelete() => ConfirmingDelete = true;

    private void CancelDelete() => ConfirmingDelete = false;

    private async Task DeleteAsync()
    {
        IsDeleting = true;
        try
        {
            await Api.DeleteTrackAsync(Id, Stopping);
            Navigation.NavigateTo(TrackListRoute);
        }
        catch (ApiException ex)
        {
            ReportDeleteError(ex.Error);
            ConfirmingDelete = false;
            IsDeleting = false;
        }
    }
}
