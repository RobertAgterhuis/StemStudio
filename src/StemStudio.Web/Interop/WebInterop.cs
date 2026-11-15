using System.Text.Json;
using Microsoft.JSInterop;

namespace StemStudio.Web.Interop;

/// <summary>Interop implementation. Red stub (T015).</summary>
/// <param name="js">Runtime.</param>
public sealed class WebInterop(IJSRuntime js) : IWebInterop, IAsyncDisposable
{
    /// <summary>Module path.</summary>
    public const string ModulePath = "./js/web-interop.js";

    /// <inheritdoc />
    public ValueTask<StoredPreferencesRead> ReadPreferencesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException(js.ToString());

    /// <inheritdoc />
    public ValueTask<bool> WritePreferencesAsync(JsonElement preferences, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask ApplyAppearanceAsync(JsonElement preferences, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> WatchPreferencesAsync(Func<string, Task> onChanged) => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> WatchSystemAppearanceAsync(Func<Task> onChanged) => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> RegisterShortcutsAsync(ShortcutScope scope, Func<string, Task> onCommand) => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> StartTourAsync(TourRuntime tour, Func<TourEnded, Task> onEnded) => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask<string?> FocusedHelpIdAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> CaptureFocusAsync() => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>JS callback receiver. Red stub (T015).</summary>
public sealed class InteropCallbacks
{
    /// <summary>Preferences.</summary>
    /// <param name="json">JSON.</param>
    /// <returns>Task.</returns>
    [JSInvokable]
    public Task OnPreferencesChanged(string json) => throw new NotImplementedException(json);

    /// <summary>System.</summary>
    /// <returns>Task.</returns>
    [JSInvokable]
    public Task OnSystemAppearanceChanged() => throw new NotImplementedException();

    /// <summary>Command.</summary>
    /// <param name="commandId">Id.</param>
    /// <returns>Task.</returns>
    [JSInvokable]
    public Task OnCommand(string commandId) => throw new NotImplementedException(commandId);

    /// <summary>Tour.</summary>
    /// <param name="tourId">Id.</param>
    /// <param name="status">Status.</param>
    /// <param name="lastStep">Step.</param>
    /// <returns>Task.</returns>
    [JSInvokable]
    public Task OnTourEnded(string tourId, string status, int lastStep) => throw new NotImplementedException(tourId + status + lastStep);
}
