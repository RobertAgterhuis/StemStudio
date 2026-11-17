using Microsoft.AspNetCore.Components;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Components.Recording;

/// <summary>Code-behind of the recording panel: forwards every control to the <see cref="RecordingSession"/>.</summary>
public sealed partial class RecordingPanel
{
    private CalibrationRun? _calibration;

    /// <summary>The mixer's recording session.</summary>
    [Parameter, EditorRequired]
    public RecordingSession Session { get; set; } = default!;

    /// <summary>Extra content (calibration dialog, takes panel).</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Raised when "Calibrate latency" is pressed.</summary>
    [Parameter]
    public EventCallback OnCalibrate { get; set; }

    private string StateName => Session.State switch
    {
        RecordingState.CountIn => "countIn",
        _ => Session.State.ToString().ToLowerInvariant(),
    };

    private string PermissionName => Session.Input.Permission.ToString().ToLowerInvariant();

    private Task AllowAsync() => RunAsync(Session.Input.AllowAsync);

    private Task RetryAsync() => RunAsync(Session.Input.RetryAsync);

    private Task DeviceAsync(string label) => RunAsync(() => Session.Input.SelectDeviceAsync(label));

    private Task ModeAsync(StemStudio.Contracts.Recording.InputChannelModeDto mode) => RunAsync(() => Session.Input.SetModeAsync(mode));

    private Task SettingsAsync(RecordingSettings settings) => RunAsync(() => Session.Input.UpdateAsync(settings));

    private Task MonitoringAsync((bool Enabled, double Volume) monitoring) =>
        RunAsync(() => Session.Input.SetMonitoringAsync(monitoring.Enabled, monitoring.Volume));

    private Task RecordAsync() => RunAsync(Session.ToggleRecordAsync);

    private Task NormalSpeedAsync() => RunAsync(Session.SetNormalSpeedAsync);

    private Task SaveAgainAsync() => RunAsync(Session.SaveAgainAsync);

    private Task OpenCalibration()
    {
        _calibration = Session.NewCalibration();
        return OnCalibrate.InvokeAsync();
    }

    private void CloseCalibration() => _calibration = null;

    private async Task RunAsync(Func<Task> action)
    {
        await action();
        await Session.RefreshAsync();
    }
}
