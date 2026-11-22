using Microsoft.AspNetCore.Components;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>Capo mode of the guitar family (016 US8, FR-075..FR-078).</summary>
public sealed partial class CapoMode
{
    /// <summary>The track.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>The 012 transpose in semitones (suggestions follow it).</summary>
    [Parameter]
    public int TransposeSemitones { get; set; }

    /// <summary>Raised with every change (and once with the stored setting).</summary>
    [Parameter]
    public EventCallback<CapoSetting> OnChanged { get; set; }
}
