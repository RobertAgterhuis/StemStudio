using Microsoft.AspNetCore.Components;
using StemStudio.Web.Mixer;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Components.Recording;

/// <summary>The take strip: forwards strip changes to the take channel and binds its meter once per loaded take.</summary>
public sealed partial class TakeStrip
{
    private string? _attachedFor;

    /// <summary>The takes and the strip settings.</summary>
    [Parameter, EditorRequired]
    public TakeLibrary Takes { get; set; } = default!;

    /// <summary>False when another channel is soloed.</summary>
    [Parameter]
    public bool Audible { get; set; } = true;

    /// <summary>Compare controls and timing adjustment.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Raised after a strip change (the mixer marks the mix changed and re-renders).</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The meter element is new each time the strip appears: bind it once per loaded take.
        if (Takes.Loaded?.Id is { } id && id != _attachedFor)
        {
            _attachedFor = id;
            await Takes.AttachMeterAsync();
        }
        else if (Takes.Loaded is null)
        {
            _attachedFor = null;
        }
    }

    private Task VolumeAsync(double volume) => ChangeAsync(s => s.Volume = volume);

    private Task PanAsync(double pan) => ChangeAsync(s => s.Pan = pan);

    private Task MuteAsync() => ChangeAsync(s => s.Mute = !s.Mute);

    private Task SoloAsync() => ChangeAsync(s => s.Solo = !s.Solo);

    private async Task ChangeAsync(Action<ChannelState> change)
    {
        await Takes.ChangeStripAsync(change);
        await OnChanged.InvokeAsync();
    }
}
