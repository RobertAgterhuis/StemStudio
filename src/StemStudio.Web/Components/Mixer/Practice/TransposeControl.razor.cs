using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer.Practice;

/// <summary>Transpose spinbutton with -1/+1 buttons and the Expert number entry.</summary>
public sealed partial class TransposeControl
{
    private const int Min = PracticeLimits.MinTransposeSemitones;
    private const int Max = PracticeLimits.MaxTransposeSemitones;

    /// <summary>The transpose heard now.</summary>
    [Parameter]
    public int Value { get; set; }

    /// <summary>True while the time-stretch stage is unavailable.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Raised with the requested transpose (the mixer clamps it).</summary>
    [Parameter]
    public EventCallback<int> OnChange { get; set; }

    private Task KeyDown(KeyboardEventArgs args) => args.Key switch
    {
        "ArrowUp" => Emit(Value + 1),
        "ArrowDown" => Emit(Value - 1),
        _ => Task.CompletedTask,
    };

    private Task NumberChanged(ChangeEventArgs args) =>
        InputValue.Parse(args) is { } value ? Emit((int)Math.Round(value)) : Task.CompletedTask;

    private Task Emit(int semitones) => Disabled ? Task.CompletedTask : OnChange.InvokeAsync(semitones);
}
