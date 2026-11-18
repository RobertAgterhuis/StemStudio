using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Mixer;
using StemStudio.Web.Recording;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Recording;

/// <summary>Part, mute-original, count-in and monitoring choices of the recording panel.</summary>
public sealed partial class RecordOptionsForm
{
    /// <summary>The current choices.</summary>
    [Parameter, EditorRequired]
    public RecordingSettings Settings { get; set; } = default!;

    /// <summary>True while recording.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Changed part, mute or count-in.</summary>
    [Parameter]
    public EventCallback<RecordingSettings> OnChange { get; set; }

    /// <summary>Changed monitoring (on/off, volume 0..1).</summary>
    [Parameter]
    public EventCallback<(bool Enabled, double Volume)> OnMonitoring { get; set; }

    private int Volume => (int)Math.Round(Settings.MonitoringVolume * 100);

    private Task PartChanged(ChangeEventArgs args) =>
        RecordingText.Parts.Where(p => WireName.Of(p.Part) == args.Value as string).Select(p => p.Part).Cast<RecordingPartDto?>().FirstOrDefault() is { } part
            ? OnChange.InvokeAsync(Settings with { Part = part })
            : Task.CompletedTask;

    private Task MuteChanged(ChangeEventArgs args) => OnChange.InvokeAsync(Settings with { MuteOriginal = args.Value is true });

    private Task CountInChanged(ChangeEventArgs args) =>
        int.TryParse(args.Value as string, out var bars) && bars is >= 0 and <= 2 ? OnChange.InvokeAsync(Settings with { CountInBars = bars }) : Task.CompletedTask;

    private Task ToggleMonitoring() => OnMonitoring.InvokeAsync((!Settings.Monitoring, Settings.MonitoringVolume));

    private Task VolumeChanged(ChangeEventArgs args) =>
        InputValue.Parse(args) is { } percent ? OnMonitoring.InvokeAsync((Settings.Monitoring, percent / 100)) : Task.CompletedTask;
}
