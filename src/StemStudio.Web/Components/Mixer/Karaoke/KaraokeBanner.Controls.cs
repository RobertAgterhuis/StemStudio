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

    /// <summary>True while the banner is switched on (<c>karaoke-toggle</c> pressed).</summary>
    public bool IsShown => Visible;

    /// <summary>True when the loaded lyrics are timed, so the banner can draw them.</summary>
    public bool HasSyncedLyrics => Karaoke is not null;

    /// <summary>Switches the banner on or off like the toggle (the composer agent's <c>show_karaoke</c>, feature 011).</summary>
    /// <param name="visible">True to show the banner.</param>
    /// <returns>A task that completes when the choice is stored and the banner re-rendered.</returns>
    public async Task SetVisibleAsync(bool visible)
    {
        await ChangeAsync(Settings with { Visible = visible });
        await InvokeAsync(StateHasChanged);
    }

    private Task ToggleAsync() => ChangeAsync(Settings with { Visible = !Visible });

    private Task OnSizeAsync(ChangeEventArgs args) =>
        args.Value is string size && KaraokeSettings.Sizes.Contains(size) ? ChangeAsync(Settings with { Size = size }) : Task.CompletedTask;

    private Task ChangeAsync(KaraokeSettings settings)
    {
        Settings = settings;
        return Choices.SaveAsync(settings);
    }
}
