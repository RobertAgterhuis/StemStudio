using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer.Practice;

/// <summary>Speed slider with -5/+5 % buttons, the heard value, the Advanced 100 % reset and the Expert number entry.</summary>
public sealed partial class SpeedControl
{
    private const int Min = PracticeLimits.MinSpeedPercent;
    private const int Max = PracticeLimits.MaxSpeedPercent;
    private const int Step = 5;

    /// <summary>The speed heard now (the trainer's speed while it runs).</summary>
    [Parameter]
    public int Value { get; set; } = 100;

    /// <summary>True while the speed trainer controls the speed: shown, not changeable (<c>aria-readonly</c>).</summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>True while the time-stretch stage is unavailable.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Raised with the requested speed (the mixer clamps it).</summary>
    [Parameter]
    public EventCallback<int> OnChange { get; set; }

    private bool Locked => ReadOnly || Disabled;

    private Task SliderInput(ChangeEventArgs args) => EmitParsed(args);

    private Task NumberChanged(ChangeEventArgs args) => EmitParsed(args);

    private Task EmitParsed(ChangeEventArgs args) =>
        InputValue.Parse(args) is { } value ? Emit((int)Math.Round(value)) : Task.CompletedTask;

    private Task Emit(int percent) => Locked ? Task.CompletedTask : OnChange.InvokeAsync(percent);
}
