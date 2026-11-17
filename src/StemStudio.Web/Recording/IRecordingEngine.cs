using Microsoft.JSInterop;

namespace StemStudio.Web.Recording;

/// <summary>
/// .NET facade over the recording part of the audio engine (audio-engine 2.5.0 "Play-along recording"). It shares the one
/// engine of <c>IAudioEngine</c> (initialized by the mixer); calls before that throw <see cref="InvalidOperationException"/>.
/// Engine rejections surface as <see cref="RecordingException"/> with the parsed <see cref="RecordingErrorCode"/>.
/// The <c>take</c> channel and the <c>take</c>/<c>input</c> meters use <c>IAudioEngine.SetChannelAsync</c> and
/// <c>IAudioEngine.AttachMeterAsync</c> like the stems.
/// </summary>
public interface IRecordingEngine
{
    /// <summary>The engine's recording callbacks.</summary>
    RecordingCallbacks Callbacks { get; }

    /// <summary>True once the shared engine exists.</summary>
    bool IsAvailable { get; }

    /// <summary><c>recordingSupport()</c>: never opens the microphone.</summary>
    /// <returns>Support and permission.</returns>
    Task<RecordingSupport> GetSupportAsync();

    /// <summary><c>listInputDevices()</c>.</summary>
    /// <returns>The audio inputs (labels empty before permission).</returns>
    Task<IReadOnlyList<InputDevice>> ListInputDevicesAsync();

    /// <summary><c>openInput(request)</c>: asks for the permission when needed.</summary>
    /// <param name="request">Device and mode.</param>
    /// <returns>The open input.</returns>
    Task<InputInfo> OpenInputAsync(OpenInputRequest request);

    /// <summary><c>closeInput()</c>: only while not recording.</summary>
    /// <returns>A task that completes when delivered.</returns>
    Task CloseInputAsync();

    /// <summary><c>setMonitoring(settings)</c>.</summary>
    /// <param name="settings">On/off and volume.</param>
    /// <returns>A task that completes when delivered.</returns>
    Task SetMonitoringAsync(MonitoringSettings settings);

    /// <summary><c>calibrateLatency(options)</c>: never throws for a failed measurement.</summary>
    /// <param name="options">Clicks and seed, or null.</param>
    /// <returns>The measurement.</returns>
    Task<CalibrationResult> CalibrateLatencyAsync(CalibrationOptions? options = null);

    /// <summary><c>startRecording(options)</c>.</summary>
    /// <param name="options">Latency, part, mute, count-in.</param>
    /// <returns>A task that completes when the capture has started.</returns>
    Task StartRecordingAsync(RecordOptions options);

    /// <summary><c>stopRecording()</c>: waits for the latency tail.</summary>
    /// <returns>Every take of the session.</returns>
    Task<IReadOnlyList<CapturedTake>> StopRecordingAsync();

    /// <summary><c>captureWav(captureKey)</c>: the 24-bit WAV as a stream reference.</summary>
    /// <param name="captureKey">The capture.</param>
    /// <returns>The stream reference (the caller disposes it).</returns>
    Task<IJSStreamReference> CaptureWavAsync(string captureKey);

    /// <summary><c>releaseCapture(captureKey)</c>; a no-op without an engine.</summary>
    /// <param name="captureKey">The capture.</param>
    /// <returns>A task that completes when delivered.</returns>
    Task ReleaseCaptureAsync(string captureKey);

    /// <summary><c>loadTake(request)</c>: creates or replaces the take channel.</summary>
    /// <param name="request">The take.</param>
    /// <returns>A task that completes when the take is scheduled.</returns>
    Task LoadTakeAsync(LoadTakeRequest request);

    /// <summary><c>unloadTake()</c>; a no-op without an engine.</summary>
    /// <returns>A task that completes when delivered.</returns>
    Task UnloadTakeAsync();

    /// <summary><c>setTakeAdjustment(samples)</c>.</summary>
    /// <param name="samples">-8820..8820.</param>
    /// <returns>A task that completes when delivered.</returns>
    Task SetTakeAdjustmentAsync(int samples);

    /// <summary><c>setTakeCompare(mode)</c>.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>A task that completes when delivered.</returns>
    Task SetTakeCompareAsync(TakeCompareMode mode);
}

/// <summary>What <see cref="RecordingInterop"/> needs from the engine wrapper: the JS engine object and the callback hub.</summary>
public interface IRecordingJsHost
{
    /// <summary>The engine object, or null before initialization.</summary>
    IJSObjectReference? RecordingEngineObject { get; }

    /// <summary>The hub the <c>[JSInvokable]</c> recording callbacks forward to.</summary>
    RecordingCallbacks RecordingCallbacks { get; }
}
