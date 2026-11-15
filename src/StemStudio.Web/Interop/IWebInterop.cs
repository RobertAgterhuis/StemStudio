using System.Text.Json;

namespace StemStudio.Web.Interop;

/// <summary>
/// C# side of the <c>web-interop.js</c> module (contracts/web-interop.md 1.0.0). Components never call
/// <c>IJSRuntime</c> for preferences, appearance, shortcuts, tours or focus directly; they use this
/// interface (faked in unit tests). Callbacks are plain delegates: the implementation owns the
/// <c>DotNetObjectReference</c> and its <c>[JSInvokable]</c> methods.
/// </summary>
public interface IWebInterop
{
    /// <summary><c>readPreferences()</c>: reads and validates the stored record (never throws in JS).</summary>
    /// <param name="cancellationToken">Cancels the interop call.</param>
    /// <returns>The validated record as JSON and whether storage works.</returns>
    ValueTask<StoredPreferencesRead> ReadPreferencesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>writePreferences(record)</c>: writes the whole record (validated, at most 16 KB).</summary>
    /// <param name="preferences">The record (data-model.md 1.1) as a JSON object.</param>
    /// <param name="cancellationToken">Cancels the interop call.</param>
    /// <returns>False when storage is unavailable or full, or the record is too large.</returns>
    ValueTask<bool> WritePreferencesAsync(JsonElement preferences, CancellationToken cancellationToken = default);

    /// <summary><c>applyAppearance(record)</c>: sets <c>data-theme/density/motion/level</c> on the root element.</summary>
    /// <param name="preferences">The record as a JSON object.</param>
    /// <param name="cancellationToken">Cancels the interop call.</param>
    /// <returns>A task that completes when applied.</returns>
    ValueTask ApplyAppearanceAsync(JsonElement preferences, CancellationToken cancellationToken = default);

    /// <summary><c>watchPreferences(dotnet)</c>: reports changes made in another tab.</summary>
    /// <param name="onChanged">Receives the new record as JSON (to be validated by the caller).</param>
    /// <returns>Disposing removes the listener.</returns>
    ValueTask<IAsyncDisposable> WatchPreferencesAsync(Func<string, Task> onChanged);

    /// <summary><c>watchSystemAppearance(dotnet)</c>: reports OS colour-scheme or reduced-motion changes.</summary>
    /// <param name="onChanged">Called after each change.</param>
    /// <returns>Disposing removes the listeners.</returns>
    ValueTask<IAsyncDisposable> WatchSystemAppearanceAsync(Func<Task> onChanged);

    /// <summary><c>registerShortcuts(dotnet, scope)</c>: registers the key map of design-spec.md 6.</summary>
    /// <param name="scope">Global or mixer key map.</param>
    /// <param name="onCommand">Receives command ids such as <c>help.open</c> or <c>mixer.channel.select:3</c>.</param>
    /// <returns>Disposing removes the key listeners.</returns>
    ValueTask<IAsyncDisposable> RegisterShortcutsAsync(ShortcutScope scope, Func<string, Task> onCommand);

    /// <summary><c>startTour(dotnet, tour)</c>: runs a tour with driver.js; hidden or missing targets are skipped.</summary>
    /// <param name="tour">Tour with texts resolved from the help topics.</param>
    /// <param name="onEnded">Called once when the tour ends.</param>
    /// <returns>Disposing destroys the running tour (for example on navigation).</returns>
    ValueTask<IAsyncDisposable> StartTourAsync(TourRuntime tour, Func<TourEnded, Task> onEnded);

    /// <summary><c>focusedHelpId()</c>: the help marker of the focused element or its closest ancestor.</summary>
    /// <param name="cancellationToken">Cancels the interop call.</param>
    /// <returns>The <c>data-help-id</c>, or <see langword="null"/>.</returns>
    ValueTask<string?> FocusedHelpIdAsync(CancellationToken cancellationToken = default);

    /// <summary><c>captureFocus()</c>: remembers the focused element.</summary>
    /// <returns>Disposing restores focus to that element.</returns>
    ValueTask<IAsyncDisposable> CaptureFocusAsync();
}
