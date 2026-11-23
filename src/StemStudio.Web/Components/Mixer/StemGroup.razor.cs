using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Insights;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Detail strips of an expanded parent (FR-012), with the 020 quality badges.</summary>
public sealed partial class StemGroup
{
    /// <summary>The expanded group.</summary>
    [Parameter, EditorRequired]
    public StemGroupState Group { get; set; } = default!;

    /// <summary>The mixer's gate mirror, used to dim silent strips.</summary>
    [Parameter, EditorRequired]
    public Func<ChannelState, bool> IsAudible { get; set; } = default!;

    /// <summary>The track (feeds the MIDI action of each strip); none hides the action.</summary>
    [Parameter]
    public TrackDto? Track { get; set; }

    /// <summary>The stem quality of a channel (020 badges); null or a null result shows no badge.</summary>
    [Parameter]
    public Func<string, StemQualityViewDto?>? Quality { get; set; }

    /// <summary>Raised with the channel id and the new gain 0..1.</summary>
    [Parameter]
    public EventCallback<ChannelValue> OnVolume { get; set; }

    /// <summary>Raised with the channel id and the new pan -1..1.</summary>
    [Parameter]
    public EventCallback<ChannelValue> OnPan { get; set; }

    /// <summary>Raised with the channel id when mute is toggled.</summary>
    [Parameter]
    public EventCallback<string> OnMute { get; set; }

    /// <summary>Raised with the channel id when solo is toggled.</summary>
    [Parameter]
    public EventCallback<string> OnSolo { get; set; }
}
