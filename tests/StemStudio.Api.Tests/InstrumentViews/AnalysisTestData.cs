using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.InstrumentViews;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.InstrumentViews;

/// <summary>Tracks, stem files and stored analyses for the instrument view tests.</summary>
internal static class AnalysisTestData
{
    public const string JobId = "4f1c2b3a4d5e6f708192a3b4c5d6e7f8";

    public static readonly DateTime T0 = SyncTestData.T0;

    /// <summary>A completed four-stem track (vocals, drums, bass, other) with an unreliable grid.</summary>
    public static Track FourStem()
    {
        var track = SyncTestData.UrlTrack();
        track.Complete(
            new TrackCompletion(
                180,
                "htdemucs",
                StemCatalog.FourStem.Select(name => new Stem(name, 10)).ToList(),
                new BeatGrid { Bpm = 100, Beats = [0.5, 1.1], Downbeats = [0.5], Confidence = 0.2, Reliable = false }),
            T0);
        return track;
    }

    /// <summary>A completed six-stem track whose drums split kept kick and snare and whose vocals were not split.</summary>
    public static Track DrumsSplitVocalsNot()
    {
        var track = SyncTestData.DetailQueued();
        track.CompleteDetail(
        [
            new ParentOutcome("vocals", ParentOutcomeKind.NotReliable, []),
            new ParentOutcome("drums", ParentOutcomeKind.Split, [new("kick", 8), new("snare", 8), new("rest", 8)]),
            new ParentOutcome("bass", ParentOutcomeKind.NotSupported, []),
            new ParentOutcome("guitar", ParentOutcomeKind.NothingFurther, []),
            new ParentOutcome("piano", ParentOutcomeKind.SkippedSilent, []),
            new ParentOutcome("other", ParentOutcomeKind.NotReliable, []),
        ],
            T0.AddMinutes(30));
        return track;
    }

    /// <summary>A completed track without any stem (impossible through the domain rules; tests the defensive prerequisite).</summary>
    public static Track WithoutStems()
    {
        var track = SyncTestData.CompletedTrack("zzzzzzzzzzz");
        var stems = typeof(Track).GetField("_stems", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        ((System.Collections.IList)stems.GetValue(track)!).Clear();
        return track;
    }

    public static ChordChart Chart(bool edited = false) => new()
    {
        Engine = "btc",
        Vocabulary = ChordVocabulary.Simple,
        Key = new SongKey(0, KeyMode.Major, 0.81, null),
        KeyRegions = [new KeyRegion(500, 16500, 0, KeyMode.Major, 0.84), new KeyRegion(16500, 32500, 2, KeyMode.Major, 0.77)],
        BeatSnapped = true,
        Segments =
        [
            new ChordSegment { StartMs = 0, EndMs = 500, Label = "N", Confidence = 0.95 },
            new ChordSegment { StartMs = 500, EndMs = 2500, Label = "C:maj", Root = 0, Quality = ChordQuality.Maj, Confidence = 0.9, Edited = edited },
            new ChordSegment { StartMs = 2500, EndMs = 4500, Label = "G:maj", Root = 7, Quality = ChordQuality.Maj, Confidence = 0.88 },
        ],
    };

    /// <summary>Stores an analysis of the document's kind (ready) with the given fingerprint.</summary>
    public static Task<TrackAnalysis> SeedReadyAsync(this ApiFactory factory, Guid trackId, AnalysisDocument document, string fingerprint) =>
        factory.WithScopeAsync(async sp =>
        {
            var kind = document switch
            {
                ChordChart => AnalysisKind.Chords,
                DrumHitsDocument => AnalysisKind.DrumHits,
                _ => AnalysisKind.VocalPitch,
            };
            var analysis = TrackAnalysis.Create(trackId, kind, T0);
            analysis.Queue(new AnalysisRun(JobId, TrackAnalysisService.DefaultOptions(kind), fingerprint, 10), T0);
            analysis.Complete(document, null, T0.AddMinutes(1));
            await sp.GetRequiredService<ITrackAnalysisRepository>().AddAsync(analysis, CancellationToken.None);
            return analysis;
        });

    /// <summary>Stores a queued analysis with the given worker job.</summary>
    public static Task<TrackAnalysis> SeedQueuedAsync(this ApiFactory factory, Guid trackId, AnalysisKind kind, string jobId) =>
        factory.WithScopeAsync(async sp =>
        {
            var analysis = TrackAnalysis.Create(trackId, kind, T0);
            analysis.Queue(new AnalysisRun(jobId, TrackAnalysisService.DefaultOptions(kind), new string('0', 64), 10), T0);
            await sp.GetRequiredService<ITrackAnalysisRepository>().AddAsync(analysis, CancellationToken.None);
            return analysis;
        });

    public static Task<TrackAnalysis?> FindAnalysisAsync(this ApiFactory factory, Guid trackId, AnalysisKind kind) =>
        factory.WithScopeAsync(sp => sp.GetRequiredService<ITrackAnalysisRepository>().FindAsync(trackId, kind, CancellationToken.None));

    /// <summary>Writes a standard stem file with the given content.</summary>
    public static Task WriteStemAsync(this ApiFactory factory, Guid trackId, string stem, byte content) =>
        factory.Services.GetRequiredService<IStemFileStore>().SaveStemAsync(trackId, stem, (s, ct) => s.WriteAsync(new[] { content }, ct).AsTask(), CancellationToken.None);

    /// <summary>Writes a detail stem file with the given content.</summary>
    public static Task WriteDetailAsync(this ApiFactory factory, Guid trackId, string channel, byte content)
    {
        var parts = channel.Split('.');
        return factory.Services.GetRequiredService<IDetailStemFileStore>()
            .SaveAsync(new DetailStemKey(trackId, parts[0], parts[1]), (s, ct) => s.WriteAsync(new[] { content }, ct).AsTask(), CancellationToken.None);
    }

    public static Task<T> WithServiceAsync<T, TService>(this ApiFactory factory, Func<TService, Task<T>> action)
        where TService : notnull =>
        factory.WithScopeAsync(sp => action(sp.GetRequiredService<TService>()));

    public static Task<bool> SyncAnalysesAsync(this ApiFactory factory) =>
        factory.WithScopeAsync(async sp =>
        {
            await sp.GetRequiredService<AnalysisJobApplier>().SyncAllAsync(CancellationToken.None);
            return true;
        });
}
