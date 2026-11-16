using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Agent;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Mixer page controls over a real <see cref="MixerState"/>; records every control call like the page's own handlers.</summary>
public sealed class FakeMixerControls : IMixerControls
{
    public FakeMixerControls(TrackDto? track = null)
    {
        Track = track ?? TestData.Track();
        State = new MixerState(Track) { Duration = Track.DurationSeconds ?? 0 };
    }

    public TrackDto Track { get; }

    public MixerState State { get; }

    public string Title => Track.Title;

    public BeatGridDto? BeatGrid => Track.BeatGrid;

    public double Position { get; set; } = 12.5;

    public bool Playing { get; set; }

    public bool KaraokeVisible { get; set; } = true;

    public bool HasSyncedLyrics { get; set; } = true;

    public List<string> Calls { get; } = [];

    public Task SetKaraokeVisibleAsync(bool visible) => Record(visible ? "karaoke on" : "karaoke off", () => KaraokeVisible = visible);

    public Task SetVolumeAsync(string stem, double volume) => Record($"volume {stem}", () => State.SetVolume(stem, volume));

    public Task SetPanAsync(string stem, double pan) => Record($"pan {stem}", () => State.SetPan(stem, pan));

    public Task ToggleMuteAsync(string stem) => Record($"mute {stem}", () => State.ToggleMute(stem));

    public Task ToggleSoloAsync(string stem) => Record($"solo {stem}", () => State.ToggleSolo(stem));

    public Task SetMasterAsync(double volume) => Record("master", () => State.SetMaster(volume));

    public Task SetMetronomeAsync(MetronomeSettingsDto settings) => Record("metronome", () => State.SetMetronome(settings));

    public Task PlayAsync() => Record("play", () => Playing = true);

    public Task PauseAsync() => Record("pause", () => Playing = false);

    public Task StopAsync() => Record("stop", () => (Playing, Position) = (false, 0));

    public Task SeekAsync(double seconds) => Record($"seek {seconds}", () => Position = seconds);

    public Task SetLoopStartAsync(double seconds) => Record("loop-start", () => State.SetLoopStart(seconds));

    public Task SetLoopEndAsync(double seconds) => Record("loop-end", () => State.SetLoopEnd(seconds));

    public Task ToggleLoopAsync() => Record("loop-toggle", () => State.ToggleLoop());

    public Task RefreshAsync() => Record("refresh", () => { });

    private Task Record(string call, Action action)
    {
        Calls.Add(call);
        action();
        return Task.CompletedTask;
    }

    private Task Record<T>(string call, Func<T> action)
    {
        Calls.Add(call);
        action();
        return Task.CompletedTask;
    }
}
