using Microsoft.AspNetCore.Components;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Channel strip of one stem; emits gains 0..1 and pans -1..1.</summary>
public sealed partial class ChannelStrip
{
    /// <summary>The channel to show.</summary>
    [Parameter, EditorRequired]
    public ChannelState Channel { get; set; } = default!;

    /// <summary>False when mute/solo silence the channel (strip is dimmed).</summary>
    [Parameter]
    public bool Audible { get; set; } = true;

    /// <summary>Raised with the new gain 0..1.</summary>
    [Parameter]
    public EventCallback<double> OnVolume { get; set; }

    /// <summary>Raised with the new pan -1..1.</summary>
    [Parameter]
    public EventCallback<double> OnPan { get; set; }

    /// <summary>Raised when mute is toggled.</summary>
    [Parameter]
    public EventCallback OnMute { get; set; }

    /// <summary>Raised when solo is toggled.</summary>
    [Parameter]
    public EventCallback OnSolo { get; set; }

    private string Volume => InputValue.Percent(Channel.Volume);

    private string Pan => InputValue.Percent(Channel.Pan);

    private string PanText => Channel.Pan switch
    {
        < 0 => $"{InputValue.Percent(-Channel.Pan)} left",
        > 0 => $"{Pan} right",
        _ => "centre",
    };

    private static string Bool(bool value) => value ? "true" : "false";

    private Task VolumeInput(ChangeEventArgs args) => Emit(OnVolume, args);

    private Task PanInput(ChangeEventArgs args) => Emit(OnPan, args);

    private static Task Emit(EventCallback<double> callback, ChangeEventArgs args) =>
        InputValue.Parse(args) is { } percent ? callback.InvokeAsync(percent / 100) : Task.CompletedTask;
}
