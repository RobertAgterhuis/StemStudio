using System.Text.Json;
using Microsoft.JSInterop;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Interop;

/// <summary>
/// <see cref="IWebInterop"/> over the ES module <c>./js/web-interop.js</c> (contracts/web-interop.md 1.0.0).
/// Scoped per circuit; the module is imported once on first use. Each registration owns its
/// <see cref="DotNetObjectReference{TValue}"/> and the JS handle, and releases both on dispose.
/// </summary>
/// <param name="js">The circuit's JS runtime.</param>
public sealed class WebInterop(IJSRuntime js) : IWebInterop, IAsyncDisposable
{
    /// <summary>Path of the module, relative to the app base.</summary>
    public const string ModulePath = "./js/web-interop.js";

    private Task<IJSObjectReference>? _module;

    /// <inheritdoc />
    public async ValueTask<StoredPreferencesRead> ReadPreferencesAsync(CancellationToken cancellationToken = default) =>
        await (await ModuleAsync()).InvokeAsync<StoredPreferencesRead>("readPreferences", cancellationToken);

    /// <inheritdoc />
    public async ValueTask<bool> WritePreferencesAsync(JsonElement preferences, CancellationToken cancellationToken = default) =>
        await (await ModuleAsync()).InvokeAsync<bool>("writePreferences", cancellationToken, preferences);

    /// <inheritdoc />
    public async ValueTask ApplyAppearanceAsync(JsonElement preferences, CancellationToken cancellationToken = default) =>
        await (await ModuleAsync()).InvokeVoidAsync("applyAppearance", cancellationToken, preferences);

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> WatchPreferencesAsync(Func<string, Task> onChanged) =>
        RegisterAsync("watchPreferences", new InteropCallbacks { Text = onChanged });

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> WatchSystemAppearanceAsync(Func<Task> onChanged) =>
        RegisterAsync("watchSystemAppearance", new InteropCallbacks { Plain = onChanged });

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> RegisterShortcutsAsync(ShortcutScope scope, Func<string, Task> onCommand) =>
        RegisterAsync("registerShortcuts", new InteropCallbacks { Text = onCommand }, scope.ToString().ToLowerInvariant());

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> RegisterShortcutsAsync(ShortcutScope scope, KeyMap keyMap, Func<string, Task> onCommand) =>
        RegisterAsync("registerShortcuts", new InteropCallbacks { Text = onCommand }, scope.ToString().ToLowerInvariant(), keyMap);

    /// <inheritdoc />
    public async ValueTask UpdateShortcutsAsync(IAsyncDisposable registration, KeyMap keyMap)
    {
        if (registration is Registration own)
        {
            var module = await ModuleAsync();
            await Registration.SafeAsync(() => module.InvokeVoidAsync("updateShortcuts", own.Handle, keyMap));
        }
    }

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> CaptureChordAsync(TimeSpan timeout, Func<KeyChordData?, Task> onCaptured) =>
        RegisterAsync("captureChord", new InteropCallbacks { Chord = onCaptured }, new CaptureOptions((int)timeout.TotalMilliseconds));

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> StartTourAsync(TourRuntime tour, Func<TourEnded, Task> onEnded) =>
        RegisterAsync("startTour", new InteropCallbacks { Tour = onEnded }, tour);

    /// <inheritdoc />
    public async ValueTask<string?> FocusedHelpIdAsync(CancellationToken cancellationToken = default) =>
        await (await ModuleAsync()).InvokeAsync<string?>("focusedHelpId", cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IAsyncDisposable> CaptureFocusAsync()
    {
        var handle = await (await ModuleAsync()).InvokeAsync<IJSObjectReference>("captureFocus");
        return new Registration(handle, null);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true } module)
        {
            await Registration.SafeAsync(() => module.Result.DisposeAsync());
        }
    }

    private Task<IJSObjectReference> ModuleAsync() =>
        _module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    private async ValueTask<IAsyncDisposable> RegisterAsync(string method, InteropCallbacks callbacks, params object?[] arguments)
    {
        var receiver = DotNetObjectReference.Create(callbacks);
        object?[] args = [receiver, .. arguments];
        var handle = await (await ModuleAsync()).InvokeAsync<IJSObjectReference>(method, args);
        return new Registration(handle, receiver);
    }

    private sealed record CaptureOptions(int TimeoutMs);

    private sealed class Registration(IJSObjectReference handle, IDisposable? receiver) : IAsyncDisposable
    {
        public IJSObjectReference Handle => handle;

        public static async ValueTask SafeAsync(Func<ValueTask> call)
        {
            try
            {
                await call();
            }
            catch (JSDisconnectedException)
            {
                // Circuit gone: the page and its listeners no longer exist.
            }
        }

        public async ValueTask DisposeAsync()
        {
            await SafeAsync(() => handle.InvokeVoidAsync("dispose"));
            await SafeAsync(() => handle.DisposeAsync());
            receiver?.Dispose();
        }
    }
}

/// <summary>
/// Receiver of the JS callbacks of one registration (web-interop.md 4). Exactly one delegate is set per
/// registration; JS only calls the method that belongs to it.
/// </summary>
public sealed class InteropCallbacks
{
    /// <summary>Receives a text payload (preferences JSON or a command id).</summary>
    internal Func<string, Task>? Text { get; init; }

    /// <summary>Receives a notification without payload.</summary>
    internal Func<Task>? Plain { get; init; }

    /// <summary>Receives the end of a tour.</summary>
    internal Func<TourEnded, Task>? Tour { get; init; }

    /// <summary>Receives a captured chord (or null).</summary>
    internal Func<KeyChordData?, Task>? Chord { get; init; }

    /// <summary>Called by <c>captureChord</c> once with the captured chord, or null after Escape or the timeout.</summary>
    /// <param name="chord">The chord.</param>
    /// <returns>The handler's task.</returns>
    [JSInvokable]
    public Task OnChordCaptured(KeyChordData? chord) => Chord!(chord);

    /// <summary>Called by <c>watchPreferences</c> with the new record of another tab.</summary>
    /// <param name="json">The validated record as JSON.</param>
    /// <returns>The handler's task.</returns>
    [JSInvokable]
    public Task OnPreferencesChanged(string json) => Text!(json);

    /// <summary>Called by <c>watchSystemAppearance</c> when the OS colour scheme or motion setting changes.</summary>
    /// <returns>The handler's task.</returns>
    [JSInvokable]
    public Task OnSystemAppearanceChanged() => Plain!();

    /// <summary>Called by <c>registerShortcuts</c> with a command id.</summary>
    /// <param name="commandId">For example <c>help.open</c>.</param>
    /// <returns>The handler's task.</returns>
    [JSInvokable]
    public Task OnCommand(string commandId) => Text!(commandId);

    /// <summary>Called by <c>startTour</c> once when the tour ends.</summary>
    /// <param name="tourId">The tour id.</param>
    /// <param name="status"><c>completed</c> or <c>dismissed</c>.</param>
    /// <param name="lastStep">Index of the last step shown.</param>
    /// <returns>The handler's task.</returns>
    [JSInvokable]
    public Task OnTourEnded(string tourId, string status, int lastStep) =>
        Tour!(new TourEnded(tourId, status == "completed", lastStep));
}
