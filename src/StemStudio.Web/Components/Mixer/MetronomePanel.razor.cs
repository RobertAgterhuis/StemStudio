using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Metronome controls; validates input with <see cref="MetronomeRules"/> and emits accepted settings.</summary>
public sealed partial class MetronomePanel
{
    /// <summary>Current settings.</summary>
    [Parameter, EditorRequired]
    public MetronomeSettingsDto Settings { get; set; } = default!;

    /// <summary>The detected beat grid (null when none).</summary>
    [Parameter]
    public BeatGridDto? Grid { get; set; }

    /// <summary>Raised with accepted settings.</summary>
    [Parameter]
    public EventCallback<MetronomeSettingsDto> OnChange { get; set; }

    /// <summary>Reset pressed.</summary>
    [Parameter]
    public EventCallback OnReset { get; set; }

    private string? Error { get; set; }

    private string? BpmText => Settings.BpmOverride is { } bpm ? InputValue.Format(bpm) : null;

    private string BpmPlaceholder => Grid is { Bpm: > 0 } grid ? InputValue.Format(grid.Bpm) : "BPM";

    private string? Hint => Grid switch
    {
        null => "No beat grid was detected. Enter a BPM to use the metronome.",
        { Reliable: false } => "The detected beat grid is not reliable, so the click starts off. Set a BPM if it drifts.",
        _ => null,
    };

    private Task ToggleAsync() => OnChange.InvokeAsync(Settings with { Enabled = !Settings.Enabled });

    private Task VolumeInput(ChangeEventArgs args) =>
        InputValue.Parse(args) is { } percent ? OnChange.InvokeAsync(MetronomeRules.Volume(Settings, percent / 100)) : Task.CompletedTask;

    private Task BpmChanged(ChangeEventArgs args) => ApplyAsync(MetronomeRules.Bpm(Settings, args.Value?.ToString()));

    private Task OffsetChanged(ChangeEventArgs args) => ApplyAsync(MetronomeRules.Offset(Settings, args.Value?.ToString()));

    private Task BeatsChanged(ChangeEventArgs args) => ApplyAsync(MetronomeRules.BeatsPerBar(Settings, args.Value?.ToString()));

    private Task ApplyAsync(MetronomeEdit edit)
    {
        Error = edit.Error;
        return edit.Settings is { } settings ? OnChange.InvokeAsync(settings) : Task.CompletedTask;
    }
}
