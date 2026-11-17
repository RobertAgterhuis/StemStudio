using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.LibraryCare;

namespace StemStudio.Web.Components.Care;

/// <summary>Undo restore (an <c>undoRestore</c> library job) and discard of the previous library (013 US2).</summary>
public sealed partial class PreviousLibraryPanel
{
    [Inject]
    private ILibraryCareClient Client { get; set; } = default!;

    /// <summary>The previous library.</summary>
    [Parameter, EditorRequired]
    public PreviousLibraryDto Previous { get; set; } = default!;

    /// <summary>A library job is queued or running (undo and discard wait).</summary>
    [Parameter]
    public bool JobRunning { get; set; }

    /// <summary>The undo job was accepted.</summary>
    [Parameter]
    public EventCallback<LibraryJobDto> OnJobStarted { get; set; }

    /// <summary>The previous library was deleted.</summary>
    [Parameter]
    public EventCallback OnDiscarded { get; set; }

    private bool Busy { get; set; }

    private bool Discarding { get; set; }

    private string? Error { get; set; }

    private string? ErrorCode { get; set; }

    private string RestoredFrom => Previous.RestoredArchive is { } archive ? $" (restored {archive})" : string.Empty;

    private async Task UndoAsync()
    {
        Busy = true;
        var (job, error) = await CareCall.TryAsync(() => Client.RevertRestoreAsync());
        Busy = false;
        (Error, ErrorCode) = (error?.UserMessage, error?.Code);
        if (job is not null)
        {
            await OnJobStarted.InvokeAsync(job);
        }
    }

    private async Task OnDiscardedAsync()
    {
        Discarding = false;
        await OnDiscarded.InvokeAsync();
    }
}
