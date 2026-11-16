using Microsoft.AspNetCore.Components;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Components.Mixer.Practice;

/// <summary>Count-in dots and number while the engine counts in; announces only the start and the end.</summary>
public sealed partial class CountInIndicator
{
    private string _announcement = string.Empty;
    private bool _wasActive;

    /// <summary>The engine's count-in progress.</summary>
    [Parameter]
    public CountInStatus Status { get; set; } = PracticeStatus.Initial.CountIn;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Status.Active != _wasActive)
        {
            _announcement = Status.Active ? "Count-in" : "Go";
            _wasActive = Status.Active;
        }
    }

    // Beats left N..1: the current beat is number total - remaining + 1.
    private string DotState(int beat)
    {
        var current = Status.BeatsTotal - Status.BeatsRemaining + 1;
        return beat < current ? "done" : beat == current ? "current" : "todo";
    }
}
