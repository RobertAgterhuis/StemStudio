using Microsoft.JSInterop;

namespace StemStudio.Web.Services;

/// <summary>Global keyboard shortcuts of the mixer (T112: space = play/pause).</summary>
public interface IKeyboardShortcuts
{
    /// <summary>
    /// Calls <paramref name="onSpace"/> when space is pressed outside text inputs, buttons and other controls.
    /// </summary>
    /// <param name="onSpace">The action.</param>
    /// <returns>A registration; dispose it to remove the listener.</returns>
    Task<IAsyncDisposable> RegisterSpaceAsync(Func<Task> onSpace);
}

/// <summary>Receives the space key from <c>wwwroot/js/mixer-keys.js</c>.</summary>
/// <param name="onSpace">The action to run.</param>
public sealed class SpaceKeyCallback(Func<Task> onSpace)
{
    /// <summary>Invoked by JavaScript on a space key press.</summary>
    /// <returns>The action's task.</returns>
    [JSInvokable]
    public Task OnSpace() => onSpace();
}

/// <summary><see cref="IKeyboardShortcuts"/> backed by the small <c>mixer-keys.js</c> module.</summary>
/// <param name="js">The circuit's JS runtime.</param>
public sealed class KeyboardShortcuts(IJSRuntime js) : IKeyboardShortcuts
{
    /// <summary>Path of the shortcut module.</summary>
    public const string ModulePath = "./js/mixer-keys.js";

    /// <inheritdoc />
    public async Task<IAsyncDisposable> RegisterSpaceAsync(Func<Task> onSpace)
    {
        var callback = DotNetObjectReference.Create(new SpaceKeyCallback(onSpace));
        var module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        var listener = await module.InvokeAsync<IJSObjectReference>("registerSpace", callback);
        return new Registration(module, listener, callback);
    }

    private sealed class Registration(IJSObjectReference module, IJSObjectReference listener, IDisposable callback) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await listener.InvokeVoidAsync("dispose");
                await listener.DisposeAsync();
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit gone: the browser page and its listener no longer exist.
            }

            callback.Dispose();
        }
    }
}
