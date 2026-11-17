using Microsoft.AspNetCore.Components;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Components.Pages;

/// <summary>
/// Shortcut settings page: Learn a chord per command (<c>captureChord</c>, 10 s), refusals named in <c>shortcut-conflict</c>,
/// clear and reset all; the pedal setup (preset, Learn left/right, pedal mode) and a test indicator that listens to the two
/// pedal keys while the page is open. Every change is saved to the preferences and applied with <c>updateShortcuts</c>.
/// </summary>
public sealed partial class ShortcutSettings : IAsyncDisposable
{
    /// <summary>How long Learn waits for a key.</summary>
    public static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);

    private static readonly PedalSide[] Sides = [PedalSide.Left, PedalSide.Right];

    private IAsyncDisposable? _capture;
    private IAsyncDisposable? _pedalTest;
    private ShortcutPreferences? _testedFor;

    [Inject]
    private IWebInterop Web { get; set; } = default!;

    /// <summary>The cascaded level.</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    private ShortcutPreferences Shortcuts => Preferences.Current.Shortcuts;

    private IReadOnlyList<ShortcutGroup> Groups => ShortcutOverview.Build(ShortcutKeys.Catalogue, Shortcuts, ["global", "mixer", "setlist", "library"]);

    private bool CanEdit => (Experience ?? ExperienceContext.Default).IsVisible("shortcuts.settings");

    private bool Custom => Shortcuts.PedalPreset == PedalPresets.Custom;

    /// <summary>The command or pedal being learned (a command id, or <c>pedal:left</c> / <c>pedal:right</c>).</summary>
    private string? Capturing { get; set; }

    private string CaptureLabel => Capturing switch
    {
        "pedal:left" => "left pedal",
        "pedal:right" => "right pedal",
        { } command => ShortcutEditor.Label(ShortcutKeys.Catalogue, command),
        _ => string.Empty,
    };

    private string? Conflict { get; set; }

    private string? PedalTest { get; set; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Dispose();
        await StopCaptureAsync();
        await (_pedalTest?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (CanEdit && !ReferenceEquals(_testedFor, Shortcuts))
        {
            await ListenToPedalsAsync();
        }
    }

    private static string SideName(PedalSide side) => side == PedalSide.Left ? "Left" : "Right";

    private static string SideId(PedalSide side) => side == PedalSide.Left ? "left" : "right";

    // The test indicator: a key map with only the two pedal keys; the mixer is not open on this page.
    private async Task ListenToPedalsAsync()
    {
        _testedFor = Shortcuts;
        await (_pedalTest?.DisposeAsync() ?? ValueTask.CompletedTask);
        var bindings = Sides.Select(s => new KeyBinding($"pedal:{SideId(s)}", [PedalPresets.KeyOf(ShortcutKeys.Catalogue, Shortcuts, s)], KeyBinding.PedalLayer, "014"));
        _pedalTest = await Web.RegisterShortcutsAsync(ShortcutScope.Mixer, new KeyMap([.. bindings], true, true), id => InvokeAsync(() => ShowPedal(id)));
    }

    private void ShowPedal(string id)
    {
        var side = id == "pedal:left" ? PedalSide.Left : PedalSide.Right;
        var command = PedalPresets.CommandOf(ShortcutKeys.Catalogue, Shortcuts, side);
        PedalTest = $"{SideName(side)} pedal pressed: {(command is null ? "no command" : ShortcutEditor.Label(ShortcutKeys.Catalogue, command))}";
        StateHasChanged();
    }

    private Task CaptureCommandAsync(string command) => StartCaptureAsync(command);

    private Task CapturePedalAsync(PedalSide side) => StartCaptureAsync($"pedal:{SideId(side)}");

    private async Task StartCaptureAsync(string target)
    {
        await StopCaptureAsync();
        (Capturing, Conflict) = (target, null);
        _capture = await Web.CaptureChordAsync(CaptureTimeout, chord => InvokeAsync(() => CapturedAsync(target, chord)));
    }

    private async Task CapturedAsync(string target, KeyChordData? chord)
    {
        Capturing = null;
        await StopCaptureAsync();
        if (chord is not null)
        {
            await ApplyAsync(target, chord);
        }

        StateHasChanged();
    }

    private Task ApplyAsync(string target, KeyChordData chord) => target.StartsWith("pedal:", StringComparison.Ordinal)
        ? LearnPedalAsync(target == "pedal:left" ? PedalSide.Left : PedalSide.Right, chord)
        : SaveAsync(ShortcutEditor.Assign(ShortcutKeys.Catalogue, Shortcuts, target, chord));

    private Task LearnPedalAsync(PedalSide side, KeyChordData chord)
    {
        var command = PedalPresets.CommandOf(ShortcutKeys.Catalogue, Shortcuts, side)!;
        var reason = KeyMapValidator.Rejection(ShortcutKeys.Catalogue, Shortcuts.Overrides, command, chord);
        return reason is null
            ? SaveAsync(new ShortcutEdit(PedalPresets.Learn(ShortcutKeys.Catalogue, Shortcuts, side, chord), null))
            : SaveAsync(ShortcutEditor.Assign(ShortcutKeys.Catalogue, Shortcuts, command, chord) with { Preferences = Shortcuts });
    }

    private async Task SaveAsync(ShortcutEdit edit)
    {
        Conflict = edit.Error;
        if (edit.Error is null)
        {
            await Preferences.SetShortcutsAsync(edit.Preferences);
        }
    }

    private async Task CancelCaptureAsync()
    {
        Capturing = null;
        await StopCaptureAsync();
    }

    private async Task StopCaptureAsync()
    {
        var capture = _capture;
        _capture = null;
        await (capture?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    private Task ClearAsync(string command)
    {
        Conflict = null;
        return Preferences.SetShortcutsAsync(ShortcutEditor.Clear(Shortcuts, command));
    }

    private Task ResetAllAsync()
    {
        Conflict = null;
        return Preferences.SetShortcutsAsync(ShortcutEditor.ResetAll(Shortcuts));
    }

    private Task PresetAsync(ChangeEventArgs args) =>
        Preferences.SetShortcutsAsync(PedalPresets.Choose(ShortcutKeys.Catalogue, Shortcuts, args.Value?.ToString() ?? string.Empty));

    private Task TogglePedalModeAsync() => Preferences.SetShortcutsAsync(Shortcuts with { PedalMode = !Shortcuts.PedalMode });
}
