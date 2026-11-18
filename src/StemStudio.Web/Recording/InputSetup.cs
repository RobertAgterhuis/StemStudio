using StemStudio.Contracts.Recording;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Recording;

/// <summary>
/// Microphone permission, device list, the open input, monitoring and the stored choices of the recording panel
/// (FR-001..FR-003, FR-005, FR-006). The microphone is requested only by <see cref="AllowAsync"/> (or on opening the
/// panel when the browser already granted it) and released by <see cref="CloseAsync"/>.
/// </summary>
/// <param name="engine">The recording engine.</param>
/// <param name="audio">The engine wrapper (meters).</param>
/// <param name="preferences">The stored choices.</param>
public sealed class InputSetup(IRecordingEngine engine, IAudioEngine audio, RecordingPreferences preferences)
{
    /// <summary>Id of the input meter element.</summary>
    public const string MeterElementId = "meter-input";

    /// <summary>The engine's support answer, or null before <see cref="LoadAsync"/>.</summary>
    public RecordingSupport? Support { get; private set; }

    /// <summary>The microphone permission as last known.</summary>
    public MicrophoneAccess Permission { get; private set; } = MicrophoneAccess.Unknown;

    /// <summary>Why recording is not possible, or null.</summary>
    public RecordingUnavailable? Unavailable { get; private set; }

    /// <summary>A readable problem that does not block the panel (e.g. a mode the device lacks), or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>The audio inputs.</summary>
    public IReadOnlyList<InputDevice> Devices { get; private set; } = [];

    /// <summary>The open input, or null.</summary>
    public InputInfo? Input { get; private set; }

    /// <summary>The stored choices.</summary>
    public RecordingSettings Settings { get; private set; } = RecordingSettings.Default;

    /// <summary>True while the permission prompt or the device is opening.</summary>
    public bool Opening { get; private set; }

    /// <summary>True when the input is open.</summary>
    public bool IsOpen => Input is not null;

    /// <summary>True when the "Allow microphone" button applies (supported, not denied, not open).</summary>
    public bool NeedsPermission => Support?.Supported == true && Input is null && Unavailable is null && Permission != MicrophoneAccess.Granted;

    /// <summary>Reads the choices and the support; opens the input at once when the permission is already granted.</summary>
    /// <returns>A task that completes when the panel state is known.</returns>
    public async Task LoadAsync()
    {
        Settings = await preferences.LoadAsync();
        Support = await engine.GetSupportAsync();
        Permission = Support.Permission;
        Unavailable = Support.Supported ? DeniedOrNull() : RecordingText.Unsupported(Support);
        await (Unavailable is null && Permission == MicrophoneAccess.Granted ? OpenAsync() : Task.CompletedTask);
    }

    /// <summary>The "Allow microphone" click: triggers the browser prompt through <c>openInput</c>.</summary>
    /// <returns>A task that completes when the input is open or refused.</returns>
    public Task AllowAsync() => OpenAsync();

    /// <summary>"Try again" after a denial or a lost device.</summary>
    /// <returns>A task that completes when the panel state is known again.</returns>
    public async Task RetryAsync()
    {
        Unavailable = null;
        Support = await engine.GetSupportAsync();
        Permission = Support.Permission == MicrophoneAccess.Denied ? MicrophoneAccess.Prompt : Support.Permission;
        await OpenAsync();
    }

    /// <summary>Opens another device (by label) with the current mode.</summary>
    /// <param name="label">The device label.</param>
    /// <returns>A task that completes when it is open.</returns>
    public Task SelectDeviceAsync(string label)
    {
        Settings = Settings with { DeviceLabel = label };
        return OpenAsync();
    }

    /// <summary>Reopens the input with another channel mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>A task that completes when it is open.</returns>
    public Task SetModeAsync(InputChannelModeDto mode)
    {
        Settings = Settings with { InputMode = mode };
        return OpenAsync();
    }

    /// <summary>Stores a changed choice (part, mute, count-in).</summary>
    /// <param name="settings">The new choices.</param>
    /// <returns>A task that completes when stored.</returns>
    public Task UpdateAsync(RecordingSettings settings)
    {
        Settings = settings;
        return preferences.SaveAsync(settings);
    }

    /// <summary>Switches monitoring or its volume (FR-006); stored and applied to the engine when the input is open.</summary>
    /// <param name="enabled">On/off.</param>
    /// <param name="volume">Gain 0..1.</param>
    /// <returns>A task that completes when applied.</returns>
    public async Task SetMonitoringAsync(bool enabled, double volume)
    {
        await UpdateAsync(Settings with { Monitoring = enabled, MonitoringVolume = Math.Clamp(volume, 0, 1) });
        await ApplyMonitoringAsync();
    }

    /// <summary>Re-lists the devices (<c>OnInputDevicesChanged</c>).</summary>
    /// <returns>A task that completes when listed.</returns>
    public async Task RefreshDevicesAsync() => Devices = await engine.ListInputDevicesAsync();

    /// <summary><c>OnInputLost</c>: the input is gone; the panel offers "Try again".</summary>
    public void MarkLost()
    {
        Input = null;
        Unavailable = RecordingText.Lost();
    }

    /// <summary>Releases the microphone (panel or mixer closes; FR-003). Never throws.</summary>
    /// <returns>A task that completes when released.</returns>
    public async Task CloseAsync()
    {
        if (Input is null)
        {
            return;
        }

        Input = null;
        try
        {
            await engine.CloseInputAsync();
        }
        catch (Exception ex) when (ex is RecordingException or InvalidOperationException)
        {
            // The engine is gone or still finishing; it releases the stream itself on dispose.
        }
    }

    private RecordingUnavailable? DeniedOrNull() => Permission == MicrophoneAccess.Denied ? RecordingText.Denied : null;

    private async Task OpenAsync()
    {
        Opening = true;
        Problem = null;
        try
        {
            await OpenDeviceAsync();
        }
        catch (RecordingException ex)
        {
            Refused(ex);
        }
        finally
        {
            Opening = false;
        }
    }

    private async Task OpenDeviceAsync()
    {
        Devices = Devices.Count > 0 ? Devices : await engine.ListInputDevicesAsync();
        var device = Devices.FirstOrDefault(d => d.Label.Length > 0 && d.Label == Settings.DeviceLabel);
        Input = await engine.OpenInputAsync(new OpenInputRequest(device?.DeviceId, Settings.InputMode));
        (Permission, Unavailable) = (MicrophoneAccess.Granted, null);
        Devices = await engine.ListInputDevicesAsync();
        await UpdateAsync(Settings with { DeviceLabel = Input.Label, InputMode = Input.Mode });
        await audio.AttachMeterAsync("input", MeterElementId);
        await ApplyMonitoringAsync();
    }

    private void Refused(RecordingException error)
    {
        Input = null;
        Unavailable = RecordingText.FromOpenError(error);
        Permission = error.Code == RecordingErrorCode.PermissionDenied ? MicrophoneAccess.Denied : Permission;
        Problem = Unavailable is null ? error.Message : null;
    }

    private Task ApplyMonitoringAsync() =>
        Input is null ? Task.CompletedTask : engine.SetMonitoringAsync(new MonitoringSettings(Settings.Monitoring, Settings.MonitoringVolume));
}
