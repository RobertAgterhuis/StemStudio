using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Domain.Tests.Tracks;

/// <summary>Tracks and per-parent outcomes for the detail phase tests.</summary>
internal static class DetailTestData
{
    public const string DetailJobId = "7c1e9a2b4d6f48e0a3b5c7d9e1f20a4b";

    public static readonly DateTime T0 = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    public static Track CompletedTrack(string model = "htdemucs_6s")
    {
        var stems = StemCatalog.ForModel(model)!.Select(name => new Stem(name, 1000)).ToList();
        var track = Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/dQw4w9WgXcQ", T0);
        track.Complete(TrackTests.Completion(model: model, stems: stems), T0);
        return track;
    }

    /// <summary>A completed track whose detail phase is queued (no worker job yet).</summary>
    public static Track Requested()
    {
        var track = CompletedTrack();
        track.RequestDetail(T0.AddMinutes(1));
        return track;
    }

    /// <summary>A completed track whose detail phase completed with drums and vocals split.</summary>
    public static Track DetailCompleted()
    {
        var track = Requested();
        track.CompleteDetail(Outcomes(), T0.AddMinutes(30));
        return track;
    }

    public static ParentOutcome Split(string parent, params string[] names) =>
        new(parent, ParentOutcomeKind.Split, names.Select(name => new DetailStem(name, 100)).ToList());

    public static ParentOutcome Unsplit(string parent, ParentOutcomeKind kind) => new(parent, kind, []);

    /// <summary>One outcome per six-stem parent: drums and vocals split, the rest not.</summary>
    public static List<ParentOutcome> Outcomes() =>
    [
        Split("vocals", "lead", "backing", "rest"),
        Split("drums", "kick", "snare", "rest"),
        Unsplit("bass", ParentOutcomeKind.NotSupported),
        Unsplit("guitar", ParentOutcomeKind.NothingFurther),
        Unsplit("piano", ParentOutcomeKind.SkippedSilent),
        Unsplit("other", ParentOutcomeKind.NotReliable),
    ];
}
