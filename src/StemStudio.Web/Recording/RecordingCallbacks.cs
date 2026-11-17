namespace StemStudio.Web.Recording;

/// <summary>
/// The five recording callbacks of audio-engine 2.5.0 as .NET events. The engine calls the <c>[JSInvokable]</c> methods of
/// <c>AudioEngineInterop</c>, which forward here; components subscribe through <see cref="IRecordingEngine.Callbacks"/>.
/// Handlers run on the circuit's thread and call <c>InvokeAsync(StateHasChanged)</c> themselves.
/// </summary>
public sealed class RecordingCallbacks
{
    /// <summary>Every recording state change.</summary>
    public event EventHandler<RecordingState>? StateChanged;

    /// <summary>At most 4 Hz while counting in or recording.</summary>
    public event EventHandler<RecordingProgress>? ProgressChanged;

    /// <summary>When a session ends (all takes of one Record press).</summary>
    public event EventHandler<IReadOnlyList<CapturedTake>>? TakesCaptured;

    /// <summary>The input device was unplugged or its track ended; the argument is the engine's reason.</summary>
    public event EventHandler<string>? InputLost;

    /// <summary>A device was added or removed.</summary>
    public event EventHandler? InputDevicesChanged;

    /// <summary>Parses an engine state; unknown values give null.</summary>
    /// <param name="state"><c>closed</c>, <c>open</c>, <c>countIn</c>, <c>recording</c> or <c>finishing</c>.</param>
    /// <returns>The state, or null.</returns>
    public static RecordingState? ParseState(string? state) =>
        Enum.TryParse<RecordingState>(state, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;

    /// <summary>Raises <see cref="StateChanged"/>; unknown states are ignored.</summary>
    /// <param name="state">The engine state string.</param>
    public void RaiseStateChanged(string state)
    {
        if (ParseState(state) is { } parsed)
        {
            StateChanged?.Invoke(this, parsed);
        }
    }

    /// <summary>Raises <see cref="ProgressChanged"/>.</summary>
    /// <param name="progress">The progress.</param>
    public void RaiseProgress(RecordingProgress progress) => ProgressChanged?.Invoke(this, progress);

    /// <summary>Raises <see cref="TakesCaptured"/>.</summary>
    /// <param name="takes">The takes of the session.</param>
    public void RaiseTakesCaptured(IReadOnlyList<CapturedTake> takes) => TakesCaptured?.Invoke(this, takes);

    /// <summary>Raises <see cref="InputLost"/>.</summary>
    /// <param name="reason">The engine's reason.</param>
    public void RaiseInputLost(string reason) => InputLost?.Invoke(this, reason);

    /// <summary>Raises <see cref="InputDevicesChanged"/>.</summary>
    public void RaiseInputDevicesChanged() => InputDevicesChanged?.Invoke(this, EventArgs.Empty);
}
