using StemStudio.Domain.Mixes;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Tests;

internal static class TestData
{
    public static readonly DateTime T0 = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    public static Track QueuedTrack(string videoId = "dQw4w9WgXcQ", DateTime? now = null) =>
        Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:" + videoId), "https://youtu.be/" + videoId, now ?? T0);

    public static Track CompletedTrack(string videoId = "dQw4w9WgXcQ")
    {
        var track = QueuedTrack(videoId);
        track.AssignJob("3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c", T0);
        track.ApplyMetadata(new TrackMetadata("Never Gonna Give You Up", "Rick Astley"), T0);
        track.Complete(Completion(), T0.AddMinutes(6));
        return track;
    }

    /// <summary>A completed track whose detail phase runs (drums, 12 %).</summary>
    public static Track RunningDetailTrack()
    {
        var track = CompletedTrack();
        track.RequestDetail(T0.AddMinutes(7));
        track.AssignDetailJob("7c1e9a2b4d6f48e0a3b5c7d9e1f20a4b", T0.AddMinutes(7));
        track.ApplyDetailProgress(new Domain.Tracks.Detail.DetailProgress(Domain.Tracks.Detail.DetailPhaseStatus.Running, "Splitting drums (1 of 3)", "drums", 12), T0.AddMinutes(8));
        return track;
    }

    public static TrackCompletion Completion() => new(
        213.04,
        "htdemucs_6s",
        StemCatalog.SixStem.Select((name, i) => new Stem(name, 1000 + i)).ToList(),
        new BeatGrid
        {
            Bpm = 113.5,
            Beats = [0.52, 1.05, 1.58, 2.11, 2.64],
            Downbeats = [0.52, 2.64],
            BeatsPerBar = 4,
            Confidence = 0.87,
            Reliable = true,
        });

    public static MixSettings Settings(string name = "Guitar practice") => new()
    {
        Name = name,
        MasterVolume = 0.9,
        Channels =
        [
            new ChannelSettings { Stem = "guitar", Volume = 0, Pan = 0, Mute = true, Solo = false },
            new ChannelSettings { Stem = "drums", Volume = 0.9, Pan = -0.2, Mute = false, Solo = true },
        ],
        Metronome = new MetronomeSettings { Enabled = true, Volume = 0.6, BpmOverride = 120.5, OffsetMs = -20, BeatsPerBar = 3 },
        Loop = new LoopRegion(10, 20, true),
    };
}
