using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.TestSupport;

internal static class SyncTestData
{
    public static readonly DateTime T0 = ApiFactory.Start.UtcDateTime;

    public static TrackCompletion Completion() => new(
        213.04,
        "htdemucs_6s",
        StemCatalog.SixStem.Select(name => new Stem(name, 10)).ToList(),
        new BeatGrid { Bpm = 113.5, Beats = [0.52, 1.05], Downbeats = [0.52], BeatsPerBar = 4, Confidence = 0.87, Reliable = true });

    public static Track UrlTrack(string videoId = "dQw4w9WgXcQ", string? jobId = "0123456789abcdef0123456789abcdef")
    {
        var track = Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:" + videoId), "https://youtu.be/" + videoId, T0);
        if (jobId is not null)
        {
            track.AssignJob(jobId, T0);
        }

        return track;
    }

    public static Track CompletedTrack(string videoId = "dQw4w9WgXcQ", DateTime? at = null)
    {
        var track = UrlTrack(videoId);
        track.Complete(Completion(), at ?? T0);
        return track;
    }

    public static Track FailedTrack(string videoId = "dQw4w9WgXcQ")
    {
        var track = UrlTrack(videoId);
        track.Fail("interrupted by restart", T0);
        return track;
    }
}
