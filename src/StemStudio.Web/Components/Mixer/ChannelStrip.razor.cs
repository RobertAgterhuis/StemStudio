using Microsoft.AspNetCore.Components;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Channel strip of one standard or detail stem; emits gains 0..1 and pans -1..1.</summary>
public sealed partial class ChannelStrip
{
    /// <summary>The channel to show.</summary>
    [Parameter, EditorRequired]
    public ChannelState Channel { get; set; } = default!;

    /// <summary>False when mute/solo silence the channel (strip is dimmed).</summary>
    [Parameter]
    public bool Audible { get; set; } = true;

    /// <summary>Expansion state of a parent with detail stems (<c>data-expanded</c>); null for strips without a group.</summary>
    [Parameter]
    public bool? Expanded { get; set; }

    /// <summary>Detail index for the tab pattern (detail strips); null for standard strips.</summary>
    [Parameter]
    public int? Pattern { get; set; }

    /// <summary>Extra content at the bottom of the strip (the expand toggle of a split parent).</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

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

    private string Name => Channel.Stem.Replace('.', ' ');

    private string StripLabel => Expanded == true ? $"{Name} channel, replaced by its detail stems" : $"{Name} channel";

    private string? ExpandedText => Expanded is { } expanded ? Bool(expanded) : null;

    private string Volume => InputValue.Percent(Channel.Volume);

    private string Pan => InputValue.Percent(Channel.Pan);

    private string PanText => Channel.Pan switch
    {
        < 0 => $"{InputValue.Percent(-Channel.Pan)} left",
        > 0 => $"{Pan} right",
        _ => "centre",
    };

    /// <summary>Read-only pan note at Beginner (pan is hidden there but stays applied), e.g. "Panned L 30".</summary>
    private string? PanMarker => Channel.Pan switch
    {
        < 0 => $"Panned L {InputValue.Percent(-Channel.Pan)}",
        > 0 => $"Panned R {Pan}",
        _ => null,
    };

    private static string Bool(bool value) => value ? "true" : "false";

    private Task VolumeInput(ChangeEventArgs args) => Emit(OnVolume, args);

    private Task PanInput(ChangeEventArgs args) => Emit(OnPan, args);

    private static Task Emit(EventCallback<double> callback, ChangeEventArgs args) =>
        InputValue.Parse(args) is { } percent ? callback.InvokeAsync(percent / 100) : Task.CompletedTask;
}
