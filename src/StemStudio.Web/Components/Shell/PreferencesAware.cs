using Microsoft.AspNetCore.Components;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Components.Shell;

/// <summary>
/// Base of components that read <see cref="PreferencesState"/> directly (level switch, preferences menu, welcome
/// panel): they re-render on every preference change even when their parent passes no new parameters.
/// </summary>
public abstract class PreferencesAware : ComponentBase, IDisposable
{
    /// <summary>The circuit's preferences.</summary>
    [Inject]
    protected PreferencesState Preferences { get; set; } = default!;

    /// <inheritdoc />
    public void Dispose()
    {
        Preferences.Changed -= OnPreferencesChanged;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void OnInitialized() => Preferences.Changed += OnPreferencesChanged;

    private void OnPreferencesChanged() => _ = InvokeAsync(StateHasChanged);
}
