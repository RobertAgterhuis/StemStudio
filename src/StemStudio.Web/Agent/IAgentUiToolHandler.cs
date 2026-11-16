using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Agent;

/// <summary>The outcome of a UI tool: a compact result or a readable error (<c>POST .../ui-results/{stepId}</c>).</summary>
/// <param name="Ok">True when the tool ran.</param>
/// <param name="Result">The result object (serialised for the backend), or null.</param>
/// <param name="Error">The readable error, or null.</param>
public sealed record UiToolResult(bool Ok, object? Result, string? Error)
{
    /// <summary>A successful result.</summary>
    /// <param name="result">The result object.</param>
    /// <returns>The outcome.</returns>
    public static UiToolResult Done(object result) => new(true, result, null);

    /// <summary>A failure.</summary>
    /// <param name="error">The readable error.</param>
    /// <returns>The outcome.</returns>
    public static UiToolResult Fail(string error) => new(false, null, error);
}

/// <summary>A set of UI tools one page (or the layout) can execute (tool catalogue 1.0.0, kind <c>ui</c>).</summary>
public interface IAgentUiToolHandler
{
    /// <summary>True when this handler owns <paramref name="tool"/>.</summary>
    /// <param name="tool">The tool name.</param>
    /// <returns>Whether it can run it.</returns>
    bool CanRun(string tool);

    /// <summary>Runs a tool.</summary>
    /// <param name="tool">The tool name.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The outcome.</returns>
    Task<UiToolResult> ExecuteAsync(string tool, UiToolArguments arguments);
}

/// <summary>
/// The mixer page's own control handlers, as the agent's mixer tools use them: every change goes through the same
/// method a click uses, so the engine, the unsaved-changes indicator and the guard behave exactly as for the user.
/// </summary>
public interface IMixerControls
{
    /// <summary>The page's mixer state.</summary>
    MixerState State { get; }

    /// <summary>The track title.</summary>
    string Title { get; }

    /// <summary>The track's beat grid, or null.</summary>
    BeatGridDto? BeatGrid { get; }

    /// <summary>The playhead in seconds.</summary>
    double Position { get; }

    /// <summary>True while playing.</summary>
    bool Playing { get; }

    /// <summary>True while the karaoke banner is switched on (<c>karaoke-toggle</c> pressed, feature 006).</summary>
    bool KaraokeVisible { get; }

    /// <summary>True when the track has synced (word or line timed) lyrics the banner can draw.</summary>
    bool HasSyncedLyrics { get; }

    /// <summary>Sets a fader (gain 0..1).</summary>
    /// <param name="stem">The channel.</param>
    /// <param name="volume">The gain.</param>
    /// <returns>A task that completes when the engine has it.</returns>
    Task SetVolumeAsync(string stem, double volume);

    /// <summary>Sets a pan (-1..1).</summary>
    /// <param name="stem">The channel.</param>
    /// <param name="pan">The pan.</param>
    /// <returns>A task that completes when the engine has it.</returns>
    Task SetPanAsync(string stem, double pan);

    /// <summary>Toggles mute.</summary>
    /// <param name="stem">The channel.</param>
    /// <returns>A task that completes when the engine has it.</returns>
    Task ToggleMuteAsync(string stem);

    /// <summary>Toggles solo.</summary>
    /// <param name="stem">The channel.</param>
    /// <returns>A task that completes when the engine has it.</returns>
    Task ToggleSoloAsync(string stem);

    /// <summary>Sets the master gain (0..1).</summary>
    /// <param name="volume">The gain.</param>
    /// <returns>A task that completes when the engine has it.</returns>
    Task SetMasterAsync(double volume);

    /// <summary>Applies metronome settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>A task that completes when the engine has them.</returns>
    Task SetMetronomeAsync(MetronomeSettingsDto settings);

    /// <summary>Starts playback.</summary>
    /// <returns>A task that completes when the engine has it.</returns>
    Task PlayAsync();

    /// <summary>Pauses playback.</summary>
    /// <returns>A task that completes when the engine has it.</returns>
    Task PauseAsync();

    /// <summary>Stops playback.</summary>
    /// <returns>A task that completes when the engine has it.</returns>
    Task StopAsync();

    /// <summary>Moves the playhead.</summary>
    /// <param name="seconds">The position.</param>
    /// <returns>A task that completes when the engine has it.</returns>
    Task SeekAsync(double seconds);

    /// <summary>Sets loop marker A.</summary>
    /// <param name="seconds">The position.</param>
    /// <returns>A task that completes when the engine has it.</returns>
    Task SetLoopStartAsync(double seconds);

    /// <summary>Sets loop marker B.</summary>
    /// <param name="seconds">The position.</param>
    /// <returns>A task that completes when the engine has it.</returns>
    Task SetLoopEndAsync(double seconds);

    /// <summary>Turns looping on or off.</summary>
    /// <returns>A task that completes when the engine has it.</returns>
    Task ToggleLoopAsync();

    /// <summary>Switches the karaoke banner on or off, as the <c>karaoke-toggle</c> button does (remembered in this browser).</summary>
    /// <param name="visible">True to show the banner.</param>
    /// <returns>A task that completes when the choice is applied.</returns>
    Task SetKaraokeVisibleAsync(bool visible);

    /// <summary>Records that the agent saved the open mixer as <paramref name="mixId"/>: the page reads the mix and, when
    /// it belongs to its track, marks it saved and loaded, as its own Save as does.</summary>
    /// <param name="mixId">The saved mix.</param>
    /// <returns>A task that completes when recorded (a mix that cannot be read changes nothing).</returns>
    Task MarkSavedAsync(Guid mixId);

    /// <summary>Re-renders the page after agent changes.</summary>
    /// <returns>A task that completes when queued.</returns>
    Task RefreshAsync();
}
