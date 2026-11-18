using Microsoft.JSInterop;

namespace StemStudio.Web.Recording;

/// <summary>JS identifiers of the recording methods (audio-engine 2.5.0); a drift test compares them with types.ts.</summary>
internal static class RecordingMethods
{
    public const string RecordingSupport = "recordingSupport";
    public const string ListInputDevices = "listInputDevices";
    public const string OpenInput = "openInput";
    public const string CloseInput = "closeInput";
    public const string SetMonitoring = "setMonitoring";
    public const string CalibrateLatency = "calibrateLatency";
    public const string StartRecording = "startRecording";
    public const string StopRecording = "stopRecording";
    public const string CaptureWav = "captureWav";
    public const string ReleaseCapture = "releaseCapture";
    public const string LoadTake = "loadTake";
    public const string UnloadTake = "unloadTake";
    public const string SetTakeAdjustment = "setTakeAdjustment";
    public const string SetTakeCompare = "setTakeCompare";

    /// <summary>Every recording method the wrapper invokes.</summary>
    public static readonly IReadOnlyList<string> All =
        [
            RecordingSupport, ListInputDevices, OpenInput, CloseInput, SetMonitoring, CalibrateLatency, StartRecording,
            StopRecording, CaptureWav, ReleaseCapture, LoadTake, UnloadTake, SetTakeAdjustment, SetTakeCompare,
        ];
}

/// <summary>
/// <see cref="IRecordingEngine"/> over the shared JS engine object (scoped, one per circuit). JS errors become
/// <see cref="RecordingException"/> with the parsed code.
/// </summary>
/// <param name="host">The engine wrapper that owns the JS object and the callbacks.</param>
public sealed class RecordingInterop(IRecordingJsHost host) : IRecordingEngine
{
    /// <inheritdoc />
    public RecordingCallbacks Callbacks => host.RecordingCallbacks;

    /// <inheritdoc />
    public bool IsAvailable => host.RecordingEngineObject is not null;

    /// <inheritdoc />
    public Task<RecordingSupport> GetSupportAsync() => Get<RecordingSupport>(RecordingMethods.RecordingSupport);

    /// <inheritdoc />
    public async Task<IReadOnlyList<InputDevice>> ListInputDevicesAsync() =>
        await Get<InputDevice[]>(RecordingMethods.ListInputDevices);

    /// <inheritdoc />
    public Task<InputInfo> OpenInputAsync(OpenInputRequest request) => Get<InputInfo>(RecordingMethods.OpenInput, request);

    /// <inheritdoc />
    public Task CloseInputAsync() => Call(RecordingMethods.CloseInput);

    /// <inheritdoc />
    public Task SetMonitoringAsync(MonitoringSettings settings) => Call(RecordingMethods.SetMonitoring, settings);

    /// <inheritdoc />
    public Task<CalibrationResult> CalibrateLatencyAsync(CalibrationOptions? options = null) =>
        Get<CalibrationResult>(RecordingMethods.CalibrateLatency, options ?? new CalibrationOptions());

    /// <inheritdoc />
    public Task StartRecordingAsync(RecordOptions options) => Call(RecordingMethods.StartRecording, options);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CapturedTake>> StopRecordingAsync() =>
        await Get<CapturedTake[]>(RecordingMethods.StopRecording);

    /// <inheritdoc />
    public Task<IJSStreamReference> CaptureWavAsync(string captureKey) =>
        Get<IJSStreamReference>(RecordingMethods.CaptureWav, captureKey);

    /// <inheritdoc />
    public Task ReleaseCaptureAsync(string captureKey) => CallIfAvailable(RecordingMethods.ReleaseCapture, captureKey);

    /// <inheritdoc />
    public Task LoadTakeAsync(LoadTakeRequest request) => Call(RecordingMethods.LoadTake, request);

    /// <inheritdoc />
    public Task UnloadTakeAsync() => CallIfAvailable(RecordingMethods.UnloadTake);

    /// <inheritdoc />
    public Task SetTakeAdjustmentAsync(int samples) => Call(RecordingMethods.SetTakeAdjustment, samples);

    /// <inheritdoc />
    public Task SetTakeCompareAsync(TakeCompareMode mode) => Call(RecordingMethods.SetTakeCompare, mode);

    private IJSObjectReference Engine =>
        host.RecordingEngineObject ?? throw new InvalidOperationException("The audio engine is not initialized.");

    private Task CallIfAvailable(string method, params object?[] args) => IsAvailable ? Call(method, args) : Task.CompletedTask;

    private async Task Call(string method, params object?[] args)
    {
        try
        {
            await Engine.InvokeVoidAsync(method, args);
        }
        catch (JSException ex)
        {
            throw RecordingException.FromEngine(ex.Message, ex);
        }
    }

    private async Task<T> Get<T>(string method, params object?[] args)
    {
        try
        {
            return await Engine.InvokeAsync<T>(method, args);
        }
        catch (JSException ex)
        {
            throw RecordingException.FromEngine(ex.Message, ex);
        }
    }
}
