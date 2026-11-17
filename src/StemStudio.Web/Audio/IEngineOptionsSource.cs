namespace StemStudio.Web.Audio;

/// <summary>
/// Supplies the options object passed as the second argument of <c>createEngine</c> (audio-engine 2.5.0
/// <c>EngineOptions</c>). Optional: <see cref="AudioEngineInterop"/> works without a registration (hook for feature 015,
/// whose <c>AddRecordingUi</c> registers an implementation).
/// </summary>
public interface IEngineOptionsSource
{
    /// <summary>The options serialized to the engine, or <see langword="null"/> to create the engine without options.</summary>
    object? Options { get; }
}
