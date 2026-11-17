using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer.Practice;

/// <summary>Count-in switch, bar count and "before every loop pass"; the mixer applies the changes.</summary>
public sealed partial class CountInControl
{
    private const string UnavailableId = "countin-unavailable-reason";

    /// <summary>The count-in settings.</summary>
    [Parameter, EditorRequired]
    public CountInSettingsDto Settings { get; set; } = CountInSettingsDto.Default;

    /// <summary>The remembered bar count shown while the count-in is off.</summary>
    [Parameter]
    public int LastBars { get; set; } = 1;

    /// <summary>True when a tempo is known (reliable grid or manual BPM).</summary>
    [Parameter]
    public bool Available { get; set; }

    /// <summary>Why the loop does not allow explicit passes, or null.</summary>
    [Parameter]
    public string? LoopProblem { get; set; }

    /// <summary>The switch was pressed.</summary>
    [Parameter]
    public EventCallback OnToggle { get; set; }

    /// <summary>Raised with a chosen bar count (1 or 2).</summary>
    [Parameter]
    public EventCallback<int> OnBars { get; set; }

    /// <summary>Raised with the new "before every loop pass" value.</summary>
    [Parameter]
    public EventCallback<bool> OnEveryPass { get; set; }

    private bool On => Settings.Bars > 0;

    // Turning on needs a tempo; a count-in that is on can always be turned off.
    private bool ToggleDisabled => !On && !Available;

    private bool EveryPassDisabled => !Settings.EveryPass && LoopProblem is not null;

    private string BarsValue => PracticeText.Number(On ? Settings.Bars : LastBars);

    private Task BarsChanged(ChangeEventArgs args) =>
        InputValue.Whole(args.Value?.ToString(), 1, PracticeLimits.MaxCountInBars) is { } bars ? OnBars.InvokeAsync(bars) : Task.CompletedTask;

    private Task EveryPassChanged(ChangeEventArgs args) => OnEveryPass.InvokeAsync(args.Value is true);
}
