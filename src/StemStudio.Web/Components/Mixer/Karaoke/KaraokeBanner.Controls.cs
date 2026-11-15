using Microsoft.AspNetCore.Components;
using StemStudio.Web.Lyrics;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Components.Mixer.Karaoke;

/// <summary>Show/hide and the text size of the banner (006 US3, FR-009, FR-011), remembered in this browser.</summary>
public sealed partial class KaraokeBanner
{
    [Inject]
    private KaraokePreferences Choices { get; set; } = default!;

    [Inject]
    private PreferencesState Motion { get; set; } = default!;

    private KaraokeSettings Settings { get; set; } = KaraokeSettings.Default;

    private bool Visible => Settings.Visible;

    private string Size => Settings.Size;

    /// <summary>True while the plain-text list of every line is shown (the banner itself is not announced).</summary>
    private bool ShowFullText { get; set; }

    private void ToggleFullText() => ShowFullText = !ShowFullText;

    private Task ToggleAsync() => ChangeAsync(Settings with { Visible = !Visible });

    private Task OnSizeAsync(ChangeEventArgs args) =>
        args.Value is string size && KaraokeSettings.Sizes.Contains(size) ? ChangeAsync(Settings with { Size = size }) : Task.CompletedTask;

    private Task ChangeAsync(KaraokeSettings settings)
    {
        Settings = settings;
        return Choices.SaveAsync(settings);
    }
}
