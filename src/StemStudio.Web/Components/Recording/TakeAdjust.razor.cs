using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Experience;
using StemStudio.Web.Mixer;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Components.Recording;

/// <summary>Code-behind of the timing adjustment: live preview, apply and reset.</summary>
public sealed partial class TakeAdjust
{
    /// <summary>The cascaded level: the note is shown from Advanced (ui-test-ids 1.14.0).</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    /// <summary>The takes (loaded take and the pending shift).</summary>
    [Parameter, EditorRequired]
    public TakeLibrary Takes { get; set; } = default!;

    /// <summary>Raised after Apply (the session polls the take until its new audio is ready).</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    private bool ShowsNote => (Experience ?? ExperienceContext.Default).Level >= ExperienceLevel.Advanced;

    private string Value => Takes.PendingAdjustmentMs.ToString("0.0", CultureInfo.InvariantCulture);

    private bool CanApply(RecordingTakeDto take) => Takes.PendingAdjustmentMs != 0 && take.Status == TakeStatusDto.Ready;

    private Task ChangedAsync(ChangeEventArgs args) =>
        InputValue.Parse(args) is { } ms ? Takes.PreviewAdjustmentAsync(ms) : Task.CompletedTask;

    private Task ResetAsync() => Takes.ResetAdjustmentAsync();

    private async Task ApplyAsync()
    {
        await Takes.ApplyAdjustmentAsync();
        await OnChanged.InvokeAsync();
    }
}
