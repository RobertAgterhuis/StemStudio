using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace StemStudio.Web.Components.Design;

/// <summary>A modal confirmation with <c>&lt;prefix&gt;-confirm</c> / <c>&lt;prefix&gt;-cancel</c> buttons (features 009/010).</summary>
public sealed partial class ConfirmModal
{
    /// <summary>Dialog title, e.g. "Delete generation".</summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>The question.</summary>
    [Parameter, EditorRequired]
    public string Message { get; set; } = string.Empty;

    /// <summary>Text of the confirming button.</summary>
    [Parameter]
    public string ConfirmText { get; set; } = "Delete";

    /// <summary>Prefix of the button ids: <c>&lt;prefix&gt;-confirm</c> and <c>&lt;prefix&gt;-cancel</c>.</summary>
    [Parameter, EditorRequired]
    public string TestIdPrefix { get; set; } = string.Empty;

    /// <summary>Confirmed; the dialog shows a busy state until the task completes.</summary>
    [Parameter]
    public EventCallback OnConfirm { get; set; }

    /// <summary>Cancel or Escape.</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <summary>Extra choices between the message and the buttons.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private bool Busy { get; set; }

    private string TitleId => $"{TestIdPrefix}-title";

    private string MessageId => $"{TestIdPrefix}-message";

    private Task OnKeyAsync(KeyboardEventArgs args) => CloseOnEscapeAsync(args, OnCancel);

    private async Task ConfirmAsync()
    {
        Busy = true;
        await OnConfirm.InvokeAsync();
        Busy = false;
    }
}
