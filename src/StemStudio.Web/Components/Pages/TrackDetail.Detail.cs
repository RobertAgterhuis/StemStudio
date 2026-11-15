using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Pages;

/// <summary>Detail phase actions of the track detail page: start/retry, cancel and the delete-blocked reason (FR-008, FR-009, FR-016).</summary>
public sealed partial class TrackDetail
{
    private string? _deleteBlocked;

    /// <summary>True while a detail start or cancel request runs.</summary>
    private bool IsDetailBusy { get; set; }

    /// <summary>Why delete was refused, shown only while the detail phase is still queued or running.</summary>
    private string? DeleteBlockedReason => DetailPhaseText.IsActive(Track!.Detail.Status) ? _deleteBlocked : null;

    private Task StartDetailAsync() => DetailActionAsync(ct => Api.StartDetailAsync(Id, ct));

    private Task CancelDetailAsync() => DetailActionAsync(ct => Api.CancelDetailAsync(Id, ct));

    private async Task DetailActionAsync(Func<CancellationToken, Task<TrackDto>> call)
    {
        IsDetailBusy = true;
        try
        {
            await SubmitDetailAsync(call);
        }
        finally
        {
            IsDetailBusy = false;
        }
    }

    private async Task SubmitDetailAsync(Func<CancellationToken, Task<TrackDto>> call)
    {
        try
        {
            Track = await call(Stopping);
            ActionError = null;
            StartPolling();
        }
        catch (ApiException ex)
        {
            ActionError = ex.Error.UserMessage;
        }
    }

    private void ReportDeleteError(ApiError error)
    {
        var blocked = error.IsConflict && DetailPhaseText.IsActive(Track!.Detail.Status);
        _deleteBlocked = blocked ? error.UserMessage : null;
        ActionError = blocked ? null : error.UserMessage;
    }
}
