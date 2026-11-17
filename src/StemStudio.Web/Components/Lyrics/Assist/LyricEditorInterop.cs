using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>Browser side of the lyrics editor (<c>wwwroot/js/lyric-editor.js</c>): the text selection and the assistant keys.</summary>
public interface ILyricEditorInterop
{
    /// <summary>Stops the browser defaults of the assistant keys (F8, Alt+Enter, Alt+Backspace, Alt+I, Ctrl+Alt+C) in an element.</summary>
    /// <param name="root">The editor element.</param>
    /// <returns>Disposing removes the listener; <see langword="null"/> while <paramref name="root"/> is not captured yet (bind again after the next render).</returns>
    Task<IAsyncDisposable?> BindKeysAsync(ElementReference root);

    /// <summary>The selection of the text area.</summary>
    /// <param name="area">The text area.</param>
    /// <returns>Start and end offsets.</returns>
    Task<(int Start, int End)> GetSelectionAsync(ElementReference area);

    /// <summary>Selects a range of the text area and focuses it.</summary>
    /// <param name="area">The text area.</param>
    /// <param name="start">Start offset.</param>
    /// <param name="finish">End offset.</param>
    /// <returns>A task that completes when selected.</returns>
    Task SelectAsync(ElementReference area, int start, int finish);

    /// <summary>Focuses an element by id.</summary>
    /// <param name="id">The element id.</param>
    /// <returns>A task that completes when focused.</returns>
    Task FocusAsync(string id);
}

/// <summary><see cref="ILyricEditorInterop"/> over the <c>lyric-editor.js</c> module.</summary>
/// <param name="js">The circuit's JS runtime.</param>
public sealed class LyricEditorInterop(IJSRuntime js) : ILyricEditorInterop, IAsyncDisposable
{
    /// <summary>Path of the module.</summary>
    public const string ModulePath = "./js/lyric-editor.js";

    private IJSObjectReference? _module;

    /// <inheritdoc />
    public async Task<IAsyncDisposable?> BindKeysAsync(ElementReference root)
    {
        // Blazor Server runs OnAfterRender after the client acknowledged the batch: the element may not be rendered yet.
        if (root.Id is null)
        {
            return null;
        }

        var listener = await (await ModuleAsync()).InvokeAsync<IJSObjectReference>("bindKeys", root);
        return new Listener(listener);
    }

    /// <inheritdoc />
    public async Task<(int Start, int End)> GetSelectionAsync(ElementReference area)
    {
        var range = await (await ModuleAsync()).InvokeAsync<int[]>("getSelection", area);
        return range is [var start, var end] ? (start, end) : (0, 0);
    }

    /// <inheritdoc />
    public async Task SelectAsync(ElementReference area, int start, int finish) => await (await ModuleAsync()).InvokeVoidAsync("select", area, start, finish);

    /// <inheritdoc />
    public async Task FocusAsync(string id) => await (await ModuleAsync()).InvokeVoidAsync("focusById", id);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone.
        }
    }

    private async Task<IJSObjectReference> ModuleAsync() => _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);

    private sealed class Listener(IJSObjectReference listener) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await listener.InvokeVoidAsync("dispose");
                await listener.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit gone: the listener no longer exists.
            }
        }
    }
}
