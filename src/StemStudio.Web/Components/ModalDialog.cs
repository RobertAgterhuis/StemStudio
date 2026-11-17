using Microsoft.AspNetCore.Components;
using StemStudio.Web.Interop;

namespace StemStudio.Web.Components;

/// <summary>
/// Base of the app's modal dialogs (export, sheet music, metadata correction; design-spec dialogs, WCAG 2.4.3): remembers
/// the element that opened the dialog, moves the focus to <see cref="InitialFocus"/> after the first render and returns it
/// to the opener when the dialog closes.
/// </summary>
public abstract class ModalDialog : ComponentBase, IAsyncDisposable
{
    private IAsyncDisposable? _returnFocus;

    /// <summary>The element that gets the focus when the dialog opens (bind it with <c>@ref</c>).</summary>
    protected ElementReference InitialFocus { get; set; }

    [Inject]
    private IWebInterop Web { get; set; } = default!;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_returnFocus is not null)
        {
            await _returnFocus.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Raises <paramref name="close"/> when the key is Escape (bind it to the dialog's <c>@onkeydown</c>).</summary>
    /// <param name="args">The key event.</param>
    /// <param name="close">The dialog's close or cancel callback.</param>
    /// <returns>A task.</returns>
    protected static Task CloseOnEscapeAsync(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs args, EventCallback close) =>
        args.Key == "Escape" ? close.InvokeAsync() : Task.CompletedTask;

    /// <summary>Moves the focus back to <see cref="InitialFocus"/> (the end sentinel of a <c>FocusTrap</c>, WCAG 2.4.3).</summary>
    /// <returns>A task.</returns>
    protected async Task WrapFocusAsync() => await InitialFocus.FocusAsync();

    /// <summary>Remembers the opener; derived dialogs call it first from their own override.</summary>
    /// <returns>A task.</returns>
    protected override async Task OnInitializedAsync() => _returnFocus = await Web.CaptureFocusAsync();

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitialFocus.FocusAsync();
        }
    }
}
