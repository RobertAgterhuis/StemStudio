using Microsoft.AspNetCore.Components;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// Base of a mixer slot component that another feature renders inside <c>Mixer.razor</c> (015: recording toolbar toggle,
/// take strip, recording area). The slot receives the hosting mixer page.
/// </summary>
public abstract class MixerSlot : ComponentBase
{
    /// <summary>The mixer page that hosts the slot.</summary>
    [Parameter, EditorRequired]
    public Mixer Mixer { get; set; } = default!;
}
