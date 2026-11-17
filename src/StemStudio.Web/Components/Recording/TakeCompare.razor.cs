using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Components.Recording;

/// <summary>Code-behind of the compare control: the radio group's keyboard pattern and the part-none switch.</summary>
public sealed partial class TakeCompare
{
    private static readonly (TakeCompareMode Mode, string Label, string Id)[] Options =
    [
        (TakeCompareMode.Take, "My take", "take-compare-take"),
        (TakeCompareMode.Original, "Original", "take-compare-original"),
        (TakeCompareMode.Both, "Both", "take-compare-both"),
    ];

    private readonly ElementReference[] _buttons = new ElementReference[3];
    private bool _focusChecked;

    /// <summary>The takes (loaded take and compare mode).</summary>
    [Parameter, EditorRequired]
    public TakeLibrary Takes { get; set; } = default!;

    /// <summary>Raised after a change.</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusChecked)
        {
            _focusChecked = false;
            await _buttons[Array.FindIndex(Options, o => o.Mode == Takes.Compare)].FocusAsync();
        }
    }

    private async Task SetAsync(TakeCompareMode mode)
    {
        await Takes.SetCompareAsync(mode);
        await OnChanged.InvokeAsync();
    }

    private Task ToggleAsync() => SetAsync(Takes.Compare == TakeCompareMode.Original ? TakeCompareMode.Both : TakeCompareMode.Original);

    // Arrow keys move the selection inside the group (WAI-ARIA radio group); other keys keep their meaning.
    private Task KeyAsync(KeyboardEventArgs args) => args.Key switch
    {
        "ArrowRight" or "ArrowDown" => MoveAsync(1),
        "ArrowLeft" or "ArrowUp" => MoveAsync(-1),
        _ => Task.CompletedTask,
    };

    private Task MoveAsync(int step)
    {
        _focusChecked = true;
        return SetAsync(Neighbour(step));
    }

    private TakeCompareMode Neighbour(int step)
    {
        var index = Array.FindIndex(Options, o => o.Mode == Takes.Compare);
        return Options[(index + step + Options.Length) % Options.Length].Mode;
    }
}
