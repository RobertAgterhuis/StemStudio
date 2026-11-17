using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.Mixer;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Components.Recording;

/// <summary>Code-behind of the calibration dialog: forwards to the <see cref="CalibrationRun"/>.</summary>
public sealed partial class CalibrationDialog
{
    private bool _manual;

    /// <summary>The run for the open input.</summary>
    [Parameter, EditorRequired]
    public CalibrationRun Run { get; set; } = default!;

    /// <summary>Close or Escape.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Raised after a calibration was stored or forgotten (the panel re-reads the latency in use).</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    private string ManualText => Run.ManualMilliseconds.ToString("0.0", CultureInfo.InvariantCulture);

    private Task OnKeyAsync(KeyboardEventArgs args) => CloseOnEscapeAsync(args, OnClose);

    private void ShowManual()
    {
        _manual = true;
        Run.SetManual(Run.Stored?.LatencyMilliseconds ?? 0);
    }

    private void ManualChanged(ChangeEventArgs args)
    {
        if (InputValue.Parse(args) is { } value)
        {
            Run.SetManual(value);
        }
    }

    private Task StartAsync() => ThenChangedAsync(Run.StartAsync);

    private Task SaveManualAsync() => ThenChangedAsync(Run.SaveManualAsync);

    private Task DeleteAsync() => ThenChangedAsync(Run.DeleteAsync);

    private async Task ThenChangedAsync(Func<Task> action)
    {
        await action();
        await OnChanged.InvokeAsync();
    }
}
