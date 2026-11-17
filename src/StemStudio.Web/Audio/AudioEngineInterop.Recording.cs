using Microsoft.JSInterop;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Audio;

/// <summary>
/// Recording part of the engine wrapper (audio-engine 2.5.0, feature 015; owned by the 015 frontend lane). The engine
/// calls these callbacks on the <c>DotNetObjectReference</c> passed to <c>createEngine</c>; they forward to
/// <see cref="RecordingCallbacks"/>. <see cref="RecordingInterop"/> uses the same engine object.
/// </summary>
public sealed partial class AudioEngineInterop : IRecordingJsHost
{
    /// <inheritdoc />
    public RecordingCallbacks RecordingCallbacks { get; } = new();

    /// <inheritdoc />
    IJSObjectReference? IRecordingJsHost.RecordingEngineObject => _engine;

    /// <summary>Engine callback <c>OnRecordingStateChanged</c> (2.5.0).</summary>
    /// <param name="state">The <c>RecordingState</c>.</param>
    [JSInvokable]
    public void OnRecordingStateChanged(string state) => RecordingCallbacks.RaiseStateChanged(state);

    /// <summary>Engine callback <c>OnRecordingProgress</c> (2.5.0, at most 4 Hz).</summary>
    /// <param name="progress">Elapsed seconds, pass and count-in beats.</param>
    [JSInvokable]
    public void OnRecordingProgress(RecordingProgress progress) => RecordingCallbacks.RaiseProgress(progress);

    /// <summary>Engine callback <c>OnTakesCaptured</c> (2.5.0).</summary>
    /// <param name="takes">The takes of the ended session.</param>
    [JSInvokable]
    public void OnTakesCaptured(CapturedTake[] takes) => RecordingCallbacks.RaiseTakesCaptured(takes);

    /// <summary>Engine callback <c>OnInputLost</c> (2.5.0).</summary>
    /// <param name="reason">The engine's reason.</param>
    [JSInvokable]
    public void OnInputLost(string reason) => RecordingCallbacks.RaiseInputLost(reason);

    /// <summary>Engine callback <c>OnInputDevicesChanged</c> (2.5.0).</summary>
    [JSInvokable]
    public void OnInputDevicesChanged() => RecordingCallbacks.RaiseInputDevicesChanged();
}
