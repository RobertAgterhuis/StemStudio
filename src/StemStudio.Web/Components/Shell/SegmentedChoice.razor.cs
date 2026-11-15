using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace StemStudio.Web.Components.Shell;

/// <summary>One option of a <see cref="SegmentedChoice{TValue}"/>.</summary>
/// <typeparam name="TValue">Value type.</typeparam>
/// <param name="Value">The value.</param>
/// <param name="Label">Visible label (also the accessible name).</param>
/// <param name="TestId"><c>data-testid</c> of the option button.</param>
public sealed record ChoiceOption<TValue>(TValue Value, string Label, string TestId);

/// <summary>A radio group rendered as segmented buttons (level switch, preference choices).</summary>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed partial class SegmentedChoice<TValue>
{
    private readonly Dictionary<int, ElementReference> _buttons = [];

    /// <summary>Options in order.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ChoiceOption<TValue>> Options { get; set; } = [];

    /// <summary>The selected value.</summary>
    [Parameter]
    public TValue? Value { get; set; }

    /// <summary>Raised with the newly selected value.</summary>
    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>Accessible name of the group.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary><c>data-testid</c> of the group, if any.</summary>
    [Parameter]
    public string? TestId { get; set; }

    private int SelectedIndex => Options.ToList().FindIndex(o => EqualityComparer<TValue>.Default.Equals(o.Value, Value));

    private string Checked(ChoiceOption<TValue> option) =>
        EqualityComparer<TValue>.Default.Equals(option.Value, Value) ? "true" : "false";

    private string TabIndex(int index) => index == Math.Max(SelectedIndex, 0) ? "0" : "-1";

    private Task SelectAsync(int index) => ValueChanged.InvokeAsync(Options[index].Value);

    private async Task KeyAsync(KeyboardEventArgs args, int index)
    {
        if (Target(args.Key, index) is not { } target)
        {
            return;
        }

        await SelectAsync(target);
        await _buttons[target].FocusAsync();
    }

    private int? Target(string key, int index) => key switch
    {
        "ArrowRight" or "ArrowDown" => (index + 1) % Options.Count,
        "ArrowLeft" or "ArrowUp" => (index + Options.Count - 1) % Options.Count,
        "Home" => 0,
        "End" => Options.Count - 1,
        _ => null,
    };
}
