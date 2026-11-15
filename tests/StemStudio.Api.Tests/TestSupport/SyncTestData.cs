using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

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

    public const string DetailJobId = "7c1e9a2b4d6f48e0a3b5c7d9e1f20a4b";

    /// <summary>A completed track whose detail phase is queued; with a worker detail job unless <paramref name="detailJobId"/> is null.</summary>
    public static Track DetailQueued(string videoId = "dQw4w9WgXcQ", string? detailJobId = DetailJobId)
    {
        var track = CompletedTrack(videoId);
        track.RequestDetail(T0);
        if (detailJobId is not null)
        {
            track.AssignDetailJob(detailJobId, T0);
        }

        return track;
    }

    /// <summary>A completed track whose detail phase completed with drums (kick, snare, rest) and vocals (lead, backing, rest) split.</summary>
    public static Track DetailCompleted(string videoId = "dQw4w9WgXcQ")
    {
        var track = DetailQueued(videoId);
        track.CompleteDetail(
        [
            new ParentOutcome("vocals", ParentOutcomeKind.Split, [new("lead", 8), new("backing", 8), new("rest", 8)]),
            new ParentOutcome("drums", ParentOutcomeKind.Split, [new("kick", 8), new("snare", 8), new("rest", 8)]),
            new ParentOutcome("bass", ParentOutcomeKind.NotSupported, []),
            new ParentOutcome("guitar", ParentOutcomeKind.NothingFurther, []),
            new ParentOutcome("piano", ParentOutcomeKind.SkippedSilent, []),
            new ParentOutcome("other", ParentOutcomeKind.NotReliable, []),
        ],
            T0.AddMinutes(30));
        return track;
    }

    public static Track FailedTrack(string videoId = "dQw4w9WgXcQ")
    {
        var track = UrlTrack(videoId);
        track.Fail("interrupted by restart", T0);
        return track;
    }
}
