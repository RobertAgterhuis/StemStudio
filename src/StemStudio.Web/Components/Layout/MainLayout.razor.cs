using Microsoft.AspNetCore.Components;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Layout;

/// <summary>
/// The app shell: loads the preferences once after the first render (one interop call), re-renders on every
/// preference change or toast, and cascades the effective <see cref="ExperienceContext"/> to the pages.
/// </summary>
public sealed partial class MainLayout : IDisposable
{
    [Inject]
    private PreferencesState Preferences { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    private ExperienceContext Experience => new(Preferences.Current.EffectiveLevel, Preferences.Current.EffectiveDensity);

    private string LevelName => Lower(Preferences.Current.EffectiveLevel);

    private string DensityName => Lower(Preferences.Current.EffectiveDensity);

    private bool HelpOpen { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        Preferences.Changed -= OnChanged;
        Toasts.Changed -= OnChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Preferences.Changed += OnChanged;
        Toasts.Changed += OnChanged;
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) =>
        firstRender ? Preferences.LoadAsync() : Task.CompletedTask;

    private static string Lower<T>(T value)
        where T : struct, Enum => value.ToString().ToLowerInvariant();

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);

    private void ToggleHelp() => HelpOpen = !HelpOpen;

    private static void OpenPalette()
    {
        // The command palette arrives with US5 (T061).
    }
}
