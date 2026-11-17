using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Practice;

/// <summary>
/// Registers the data-driven key map of a scope (web-interop 1.1.0) built from the user's shortcut preferences and keeps
/// it current: when the shortcut preferences change (settings page, Preferences switch, another tab) every live
/// registration gets the new map through <c>updateShortcuts</c>. Scoped per circuit.
/// </summary>
/// <param name="web">The web-interop module.</param>
/// <param name="preferences">The preferences state.</param>
public sealed class ShortcutKeys(IWebInterop web, PreferencesState preferences)
{
    private IWebInterop Web { get; } = web;

    private PreferencesState Preferences { get; } = preferences;

    /// <summary>The catalogue used for every key map.</summary>
    public static KeyMapCatalogue Catalogue => KeyMapDefaults.Catalogue;

    /// <summary>The effective key map of a scope for the current preferences.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The key map.</returns>
    public KeyMap MapOf(ShortcutScope scope) => KeyMapBuilder.Build(Catalogue, Preferences.Current.Shortcuts, ScopeName(scope));

    /// <summary>Registers the key map of a scope.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="onCommand">Receives command ids.</param>
    /// <returns>Disposing removes the key listener and stops following the preferences.</returns>
    public async Task<IAsyncDisposable> RegisterAsync(ShortcutScope scope, Func<string, Task> onCommand)
    {
        var shortcuts = Preferences.Current.Shortcuts;
        var registration = await Web.RegisterShortcutsAsync(scope, MapOf(scope), onCommand);
        return new Live(this, scope, registration, shortcuts);
    }

    /// <summary>The catalogue scope name of a scope.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns><c>global</c>, <c>mixer</c>, <c>library</c> or <c>setlist</c>.</returns>
    public static string ScopeName(ShortcutScope scope) => scope.ToString().ToLowerInvariant();

    private sealed class Live : IAsyncDisposable
    {
        private readonly ShortcutKeys _owner;
        private readonly ShortcutScope _scope;
        private readonly IAsyncDisposable _registration;
        private ShortcutPreferences _applied;

        public Live(ShortcutKeys owner, ShortcutScope scope, IAsyncDisposable registration, ShortcutPreferences applied)
        {
            (_owner, _scope, _registration, _applied) = (owner, scope, registration, applied);
            owner.Preferences.Changed += OnChanged;
        }

        public ValueTask DisposeAsync()
        {
            _owner.Preferences.Changed -= OnChanged;
            return _registration.DisposeAsync();
        }

        private void OnChanged()
        {
            var current = _owner.Preferences.Current.Shortcuts;
            if (!ReferenceEquals(current, _applied))
            {
                _applied = current;
                _ = _owner.Web.UpdateShortcutsAsync(_registration, _owner.MapOf(_scope)).AsTask();
            }
        }
    }
}
