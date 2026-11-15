using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Shell;

/// <summary>Preferences button and its non-modal panel; Escape closes it and returns focus to the button.</summary>
public sealed partial class PreferencesMenu
{
    private static readonly IReadOnlyList<ChoiceOption<ThemePreference>> Themes =
    [
        new(ThemePreference.System, "System", "pref-theme-system"),
        new(ThemePreference.Light, "Light", "pref-theme-light"),
        new(ThemePreference.Dark, "Dark", "pref-theme-dark"),
    ];

    private static readonly IReadOnlyList<ChoiceOption<DensityPreference>> Densities =
    [
        new(DensityPreference.Auto, "Level default", "pref-density-auto"),
        new(DensityPreference.Comfortable, "Comfortable", "pref-density-comfortable"),
        new(DensityPreference.Compact, "Compact", "pref-density-compact"),
    ];

    private static readonly IReadOnlyList<ChoiceOption<MotionPreference>> Motions =
    [
        new(MotionPreference.System, "System", "pref-motion-system"),
        new(MotionPreference.Reduced, "Reduced", "pref-motion-reduced"),
    ];

    private ElementReference _button;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    private bool Open { get; set; }

    private void Toggle() => Open = !Open;

    private async Task KeyAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            Open = false;
            await _button.FocusAsync();
        }
    }

    private async Task ResetHelpAsync()
    {
        await Preferences.ResetHelpAsync();
        Toasts.Show("Help and tours reset");
    }
}
