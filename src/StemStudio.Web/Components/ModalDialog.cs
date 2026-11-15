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
