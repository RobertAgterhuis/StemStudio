using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Audio;

/// <summary>Transport state reported by the engine (<c>PlaybackState</c> in types.ts).</summary>
public enum PlaybackState
{
    /// <summary>Not playing; position 0.</summary>
    Stopped,

    /// <summary>Playing.</summary>
    Playing,

    /// <summary>Paused at the current position.</summary>
    Paused,
}

/// <summary>
/// .NET facade over the browser audio engine (<c>contracts/audio-engine.md</c>). All calls after
/// <see cref="InitializeAsync"/> forward to the JS engine; engine errors surface as <see cref="AudioEngineException"/>.
/// <see cref="IAsyncDisposable.DisposeAsync"/> releases the engine; it can be initialized again afterwards.
/// </summary>
public interface IAudioEngine : IAsyncDisposable
{
    /// <summary>Raised at most 10 Hz while playing and once after seek, pause and stop (seconds).</summary>
    event EventHandler<double>? PositionChanged;

    /// <summary>Raised on every transport state change.</summary>
    event EventHandler<PlaybackState>? PlaybackStateChanged;

    /// <summary>True between <see cref="InitializeAsync"/> and disposal.</summary>
    bool IsInitialized { get; }

    /// <summary>Imports the engine module and creates one engine (idempotent).</summary>
    /// <returns>A task that completes when the engine exists.</returns>
    Task InitializeAsync();

    /// <summary>Downloads and decodes all stems of a completed track and registers its detail groups (loaded lazily).</summary>
    /// <param name="track">A completed track.</param>
    /// <returns>The duration in seconds.</returns>
    Task<double> LoadTrackAsync(TrackDto track);

    /// <summary>Starts all stems sample-locked.</summary>
    /// <returns>A task that completes when playback has started.</returns>
    Task PlayAsync();

    /// <summary>Pauses and keeps the position.</summary>
    /// <returns>A task that completes when the call is delivered.</returns>
    Task PauseAsync();

    /// <summary>Stops and resets the position to 0.</summary>
    /// <returns>A task that completes when the call is delivered.</returns>
    Task StopAsync();

    /// <summary>Jumps to a position.</summary>
    /// <param name="seconds">Target position in seconds.</param>
    /// <returns>A task that completes when the call is delivered.</returns>
    Task SeekAsync(double seconds);

    /// <summary>Applies one channel's volume, pan, mute and solo.</summary>
    /// <param name="channel">The channel settings; <c>Stem</c> is the channel id (stem name or <c>&lt;parent&gt;.&lt;detail&gt;</c>).</param>
    /// <returns>A task that completes when the call is delivered.</returns>
    Task SetChannelAsync(ChannelSettingsDto channel);

    /// <summary>
    /// Expands (detail stems replace the parent; the group is loaded on first use) or collapses a split parent
    /// (engine contract 2.0.0). A failing group load throws and the parent stays collapsed.
    /// </summary>
    /// <param name="parent">A parent with a detail group.</param>
    /// <param name="expanded">The new state.</param>
    /// <returns>A task that completes when the swap is scheduled.</returns>
    Task SetExpandedAsync(string parent, bool expanded);

    /// <summary>Sets the master gain 0..1.</summary>
    /// <param name="volume">The gain.</param>
    /// <returns>A task that completes when the call is delivered.</returns>
    Task SetMasterAsync(double volume);

    /// <summary>Sets or clears the A-B loop.</summary>
    /// <param name="region">A valid region (0 &lt;= start &lt; end &lt;= duration).</param>
    /// <returns>A task that completes when the call is delivered.</returns>
    Task SetLoopAsync(LoopRegionDto region);

    /// <summary>Applies metronome settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>A task that completes when the call is delivered.</returns>
    Task SetMetronomeAsync(MetronomeSettingsDto settings);

    /// <summary>Binds a peak meter to a DOM element.</summary>
    /// <param name="target">A channel id (stem name or <c>&lt;parent&gt;.&lt;detail&gt;</c>) or <c>master</c>.</param>
    /// <param name="elementId">The element id.</param>
    /// <returns>A task that completes when the call is delivered.</returns>
    Task AttachMeterAsync(string target, string elementId);
}
