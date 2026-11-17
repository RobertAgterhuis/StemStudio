using Microsoft.AspNetCore.Components;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Care;

/// <summary>
/// Base of the library care dialogs (backup, restore, clean-up, discard; feature 013): the library care client, the busy
/// flag of the running call and the readable reason plus problem code of the last failed call.
/// </summary>
public abstract class CareDialog : ModalDialog
{
    /// <summary>The library care API client.</summary>
    [Inject]
    protected ILibraryCareClient Client { get; set; } = default!;

    /// <summary>The readable reason of the last failed call (null when it succeeded).</summary>
    protected string? Error { get; private set; }

    /// <summary>The problem code of <see cref="Error"/>.</summary>
    protected string? ErrorCode { get; private set; }

    /// <summary>True while the dialog's main call runs.</summary>
    protected bool Busy { get; set; }

    /// <summary>Shows the failure of a call, or clears it when <paramref name="error"/> is null.</summary>
    /// <param name="error">The failure, or null.</param>
    protected void ShowError(ApiError? error) => (Error, ErrorCode) = (error?.UserMessage, error?.Code);
}
