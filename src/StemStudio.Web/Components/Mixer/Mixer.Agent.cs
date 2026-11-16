using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Agent;
using StemStudio.Web.Audio;
using StemStudio.Web.Components.Mixer.Karaoke;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// Registers the composer agent's mixer UI tools for this page's track (feature 011, DELTA.md section 4). The tools
/// call the page's own control handlers, so the engine, the unsaved indicator and the leave guard are unchanged.
/// </summary>
public sealed partial class Mixer
{
    private IDisposable? _agentTools;
    private KaraokeBanner? _karaoke;

    [Inject]
    private AgentUiToolHost AgentTools { get; set; } = default!;

    private void RegisterAgentTools() => _agentTools = AgentTools.RegisterMixer(new MixerUiTools(new AgentControls(this)));

    private void ReleaseAgentTools() => _agentTools?.Dispose();

    /// <summary>The page's handlers as <see cref="IMixerControls"/>.</summary>
    /// <param name="page">The mixer page.</param>
    private sealed class AgentControls(Mixer page) : IMixerControls
    {
        public MixerState State => page.State!;

        public string Title => page.Title;

        public BeatGridDto? BeatGrid => page._track!.BeatGrid;

        public double Position => page.Position;

        public bool Playing => page.Playback == PlaybackState.Playing;

        // The tools are registered after the first render with a loaded track, so the banner exists.
        public bool KaraokeVisible => page._karaoke!.IsShown;

        public bool HasSyncedLyrics => page._karaoke!.HasSyncedLyrics;

        public Task SetKaraokeVisibleAsync(bool visible) => page._karaoke!.SetVisibleAsync(visible);

        public Task SetVolumeAsync(string stem, double volume) => page.SetVolumeAsync(stem, volume);

        public Task SetPanAsync(string stem, double pan) => page.SetPanAsync(stem, pan);

        public Task ToggleMuteAsync(string stem) => page.ToggleMuteAsync(stem);

        public Task ToggleSoloAsync(string stem) => page.ToggleSoloAsync(stem);

        public Task SetMasterAsync(double volume) => page.SetMasterAsync(volume);

        public Task SetMetronomeAsync(MetronomeSettingsDto settings) => page.SetMetronomeAsync(settings);

        public Task PlayAsync() => page.PlayAsync();

        public Task PauseAsync() => page.PauseAsync();

        public Task StopAsync() => page.StopAsync();

        public Task SeekAsync(double seconds) => page.SeekAsync(seconds);

        public Task SetLoopStartAsync(double seconds) => page.SetLoopStartAsync(seconds);

        public Task SetLoopEndAsync(double seconds) => page.SetLoopEndAsync(seconds);

        public Task ToggleLoopAsync() => page.ToggleLoopAsync();

        public Task MarkSavedAsync(Guid mixId) => page.InvokeAsync(() => page.MarkSavedByAgentAsync(mixId));

        public Task RefreshAsync() => page.InvokeAsync(page.StateHasChanged);
    }
}
