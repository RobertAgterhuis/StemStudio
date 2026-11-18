using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using static StemStudio.Domain.Tests.InstrumentViews.ChartData;

namespace StemStudio.Domain.Tests.InstrumentViews;

/// <summary>Feature 016 T039: the status transitions of data-model 7.2, fingerprints, auto rules 7.3 and the edit guard.</summary>
public sealed class TrackAnalysisTests
{
    private const string JobId = "4f1c2b3a4d5e6f708192a3b4c5d6e7f8";
    private static readonly Guid TrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");
    private static readonly DateTime T0 = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
    private static readonly AnalysisRun Run = new(JobId, new AnalysisOptions(ChordVocabulary.Simple, null), new string('a', 64), 12);
    private static readonly JobDevice Cpu = new(ProcessingDeviceKind.Cpu, null, false, null);

    private static TrackAnalysis Ready()
    {
        var analysis = TrackAnalysis.Create(TrackId, AnalysisKind.Chords, T0);
        analysis.Queue(Run, T0);
        analysis.Complete(Chart(("C:maj", 1000), ("G:maj", 1000)), Cpu, T0.AddSeconds(5));
        return analysis;
    }

    [Fact]
    public void Create_IsNoneWithoutJobOrDocument()
    {
        var analysis = TrackAnalysis.Create(TrackId, AnalysisKind.DrumHits, T0);

        (analysis.TrackId, analysis.Kind, analysis.Status, analysis.UpdatedAt).ShouldBe((TrackId, AnalysisKind.DrumHits, AnalysisStatus.None, T0));
        (analysis.JobId, analysis.Document, analysis.Options).ShouldBe((null, null, AnalysisOptions.Empty));
        analysis.CheckStart(false).ShouldBe(StartRefusal.None);
    }

    [Fact]
    public void Queue_StoresTheRunAndClearsTheDocument()
    {
        var analysis = Ready();

        analysis.Queue(Run with { EstimateSeconds = 30 }, T0.AddMinutes(1));

        (analysis.Status, analysis.JobId, analysis.Progress, analysis.EstimateSeconds).ShouldBe((AnalysisStatus.Queued, JobId, 0d, 30));
        (analysis.Document, analysis.Engine, analysis.Reason).ShouldBe((null, null, null));
        analysis.SourceFingerprint.ShouldBe(new string('a', 64));
        analysis.Options.Vocabulary.ShouldBe(ChordVocabulary.Simple);
        analysis.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Queue_RejectsABadJobId() =>
        Should.Throw<ArgumentException>(() => TrackAnalysis.Create(TrackId, AnalysisKind.Chords, T0).Queue(Run with { JobId = "../x" }, T0));

    [Fact]
    public void Progress_GoesToRunningAndNeverBack()
    {
        var analysis = TrackAnalysis.Create(TrackId, AnalysisKind.Chords, T0);
        analysis.Queue(Run, T0);

        analysis.ApplyProgress(false, 0.1, null, T0);
        analysis.Status.ShouldBe(AnalysisStatus.Queued);
        analysis.ApplyProgress(true, 0.42, Cpu, T0.AddSeconds(1));
        analysis.ApplyProgress(true, 0.3, null, T0.AddSeconds(2));
        analysis.ApplyProgress(true, 7, null, T0.AddSeconds(3));

        (analysis.Status, analysis.Progress, analysis.Device).ShouldBe((AnalysisStatus.Running, 1d, Cpu));
        analysis.CheckStart(true).ShouldBe(StartRefusal.AnalysisRunning);
    }

    [Fact]
    public void Progress_IsIgnoredWhenNotActive()
    {
        var analysis = Ready();

        analysis.ApplyProgress(true, 0.5, null, T0.AddHours(1));

        (analysis.Status, analysis.UpdatedAt).ShouldBe((AnalysisStatus.Ready, T0.AddSeconds(5)));
    }

    [Fact]
    public void Complete_StoresTheDocumentAndItsEngine()
    {
        var analysis = Ready();

        (analysis.Status, analysis.Progress, analysis.Engine, analysis.JobId, analysis.EstimateSeconds).ShouldBe((AnalysisStatus.Ready, 1d, "fake", null, null));
        analysis.Document.ShouldBeOfType<ChordChart>().Segments.Count.ShouldBe(2);
        analysis.Device.ShouldBe(Cpu);
    }

    [Fact]
    public void Complete_RejectsNull() => Should.Throw<ArgumentNullException>(() => Ready().Complete(null!, null, T0));

    [Fact]
    public void Fail_KeepsProgressAndStoresTheReason()
    {
        var analysis = TrackAnalysis.Create(TrackId, AnalysisKind.VocalPitch, T0);
        analysis.Queue(Run, T0);
        analysis.ApplyProgress(true, 0.3, null, T0);

        analysis.Fail(new string('x', 1200), T0.AddSeconds(9));

        (analysis.Status, analysis.Progress, analysis.JobId).ShouldBe((AnalysisStatus.Failed, 0.3, null));
        analysis.Reason!.Length.ShouldBe(TrackAnalysis.MaxReasonLength);
        analysis.CheckStart(false).ShouldBe(StartRefusal.None);
    }

    [Fact]
    public void Fail_WithoutText_UsesTheDefault()
    {
        var analysis = Ready();

        analysis.Fail(null, T0);

        analysis.Reason.ShouldBe(TrackAnalysis.DefaultFailure);
    }

    [Fact]
    public void Unavailable_ClearsTheDocumentAndIsIdempotent()
    {
        var analysis = Ready();

        analysis.MarkUnavailable("Split the drums first.", T0.AddMinutes(1));
        analysis.MarkUnavailable("Split the drums first.", T0.AddMinutes(2));

        (analysis.Status, analysis.Reason, analysis.Document, analysis.UpdatedAt).ShouldBe((AnalysisStatus.Unavailable, "Split the drums first.", null, T0.AddMinutes(1)));
    }

    [Fact]
    public void Stale_OnlyFromReadyAndOnlyWhenTheFingerprintChanged()
    {
        var analysis = Ready();

        analysis.MarkStaleIfChanged(new string('a', 64), T0).ShouldBeFalse();
        analysis.MarkStaleIfChanged(new string('b', 64), T0.AddMinutes(1)).ShouldBeTrue();
        analysis.MarkStaleIfChanged(new string('c', 64), T0.AddMinutes(2)).ShouldBeFalse();

        (analysis.Status, analysis.Reason).ShouldBe((AnalysisStatus.Stale, TrackAnalysis.StaleReason));
        analysis.Document.ShouldNotBeNull();
    }

    [Fact]
    public void ClearAndReset_GoBackToNone()
    {
        var analysis = Ready();
        analysis.Clear(T0.AddMinutes(1));
        (analysis.Status, analysis.Document, analysis.SourceFingerprint, analysis.Progress).ShouldBe((AnalysisStatus.None, null, null, 0d));

        analysis.Queue(Run, T0.AddMinutes(2));
        analysis.ResetToNone(T0.AddMinutes(3));
        (analysis.Status, analysis.JobId).ShouldBe((AnalysisStatus.None, null));
    }

    [Fact]
    public void Edits_MarkTheSegmentAndGuardARerun()
    {
        var analysis = Ready();

        analysis.EditChord(1, "A:min7/b7", T0.AddMinutes(1)).ShouldBeTrue();

        var segment = analysis.Document.ShouldBeOfType<ChordChart>().Segments[1];
        (segment.Label, segment.Root, segment.Quality, segment.Bass, segment.Edited).ShouldBe(("A:min7/b7", 9, ChordQuality.Min7, 7, true));
        analysis.HasEdits.ShouldBeTrue();
        analysis.CheckStart(false).ShouldBe(StartRefusal.EditsExist);
        analysis.CheckStart(true).ShouldBe(StartRefusal.None);
        analysis.UpdatedAt.ShouldBe(T0.AddMinutes(1));
    }

    [Fact]
    public void Edit_ToNoChord_ClearsRootQualityAndBass()
    {
        var analysis = Ready();

        analysis.EditChord(0, "N", T0).ShouldBeTrue();

        analysis.Document.ShouldBeOfType<ChordChart>().Segments[0].Chord.ShouldBe(ChordName.None);
    }

    [Fact]
    public void Edit_OfAStaleChart_IsAllowed()
    {
        var analysis = Ready();
        analysis.MarkStaleIfChanged(new string('b', 64), T0);

        analysis.EditChord(0, "D:maj", T0).ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1, "C:maj", "segmentIndex")]
    [InlineData(2, "C:maj", "segmentIndex")]
    [InlineData(0, "C:power", "label")]
    public void Edit_WithABadIndexOrLabel_Throws(int index, string label, string field)
    {
        var error = Should.Throw<DomainValidationException>(() => Ready().EditChord(index, label, T0));

        error.Errors.ShouldContain(e => e.Field == field);
    }

    [Fact]
    public void Edit_WithoutAReadyChart_IsRefused()
    {
        var queued = TrackAnalysis.Create(TrackId, AnalysisKind.Chords, T0);
        queued.Queue(Run, T0);
        queued.EditChord(0, "C:maj", T0).ShouldBeFalse();

        var drums = TrackAnalysis.Create(TrackId, AnalysisKind.DrumHits, T0);
        drums.Queue(Run, T0);
        drums.Complete(new DrumHitsDocument { Engine = "fake", Pieces = [DrumPiece.Kick], Hits = [new DrumHit(0, DrumPiece.Kick, 1)] }, null, T0);
        drums.EditChord(0, "C:maj", T0).ShouldBeFalse();
        drums.HasEdits.ShouldBeFalse();
    }

    [Fact]
    public void SpellingKey_IsTheRegionKeyElseTheGlobalKey()
    {
        var chart = Chart(("C:maj", 1000)) with { KeyRegions = [new KeyRegion(500, 1000, 2, KeyMode.Major, 0.8)] };

        chart.SpellingKeyAt(600).ShouldBe(new MusicalKey(2, KeyMode.Major));
        chart.SpellingKeyAt(100).ShouldBe(new MusicalKey(0, KeyMode.Major));
        (chart with { Key = null }).SpellingKeyAt(100).ShouldBeNull();
    }

    [Fact]
    public void Segment_ReportsDurationAndChord()
    {
        var segment = Segment(500, 2500, "C:maj/3");

        (segment.DurationMs, segment.HasChord, segment.Chord).ShouldBe((2000, true, new ChordName(0, ChordQuality.Maj, 4)));
        Segment(0, 1, "X").HasChord.ShouldBeFalse();
    }

    [Theory]
    [InlineData(AnalysisKind.Chords, AnalysisTrigger.StandardCompleted, true, false, true)]
    [InlineData(AnalysisKind.Chords, AnalysisTrigger.StandardCompleted, false, true, false)]
    [InlineData(AnalysisKind.Chords, AnalysisTrigger.DetailCompleted, true, true, false)]
    [InlineData(AnalysisKind.DrumHits, AnalysisTrigger.DetailCompleted, false, true, true)]
    [InlineData(AnalysisKind.DrumHits, AnalysisTrigger.DetailCompleted, true, false, false)]
    [InlineData(AnalysisKind.DrumHits, AnalysisTrigger.StandardCompleted, true, true, false)]
    [InlineData(AnalysisKind.VocalPitch, AnalysisTrigger.StandardCompleted, true, true, false)]
    [InlineData(AnalysisKind.VocalPitch, AnalysisTrigger.DetailCompleted, true, true, false)]
    public void AutoRules_FollowTheTriggersAndSwitches(AnalysisKind kind, AnalysisTrigger trigger, bool chords, bool drums, bool expected) =>
        AnalysisRules.FollowsTrigger(kind, trigger, new AutoRuleSettings(chords, drums)).ShouldBe(expected);

    [Theory]
    [InlineData(AnalysisKind.Chords, true, false, true)]
    [InlineData(AnalysisKind.Chords, false, true, false)]
    [InlineData(AnalysisKind.DrumHits, false, true, true)]
    [InlineData(AnalysisKind.DrumHits, true, false, false)]
    [InlineData(AnalysisKind.VocalPitch, true, true, false)]
    public void StaleReruns_FollowTheSwitches(AnalysisKind kind, bool chords, bool drums, bool expected) =>
        AnalysisRules.RerunsWhenStale(kind, new AutoRuleSettings(chords, drums)).ShouldBe(expected);

    [Fact]
    public void AutoRules_RejectNullSettings()
    {
        Should.Throw<ArgumentNullException>(() => AnalysisRules.FollowsTrigger(AnalysisKind.Chords, AnalysisTrigger.StandardCompleted, null!));
        Should.Throw<ArgumentNullException>(() => AnalysisRules.RerunsWhenStale(AnalysisKind.Chords, null!));
    }

    [Fact]
    public void AutomaticStarts_OnlyReplaceNoneUnavailableOrStaleWithoutEdits()
    {
        AnalysisRules.MayStartAutomatically(null).ShouldBeTrue();
        AnalysisRules.MayStartAutomatically(TrackAnalysis.Create(TrackId, AnalysisKind.Chords, T0)).ShouldBeTrue();
        var ready = Ready();
        AnalysisRules.MayStartAutomatically(ready).ShouldBeFalse();
        ready.MarkStaleIfChanged(new string('b', 64), T0);
        AnalysisRules.MayStartAutomatically(ready).ShouldBeTrue();
        ready.EditChord(0, "D:maj", T0);
        AnalysisRules.MayStartAutomatically(ready).ShouldBeFalse();
        ready.MarkUnavailable("No harmonic stem.", T0);
        AnalysisRules.MayStartAutomatically(ready).ShouldBeTrue();
        ready.Fail("x", T0);
        AnalysisRules.MayStartAutomatically(ready).ShouldBeFalse();
    }

    [Fact]
    public void Fingerprint_IsOrderIndependentAndChangesWithAnInput()
    {
        var a = AnalysisRules.Fingerprint([new AnalysisInput("bass", "11"), new AnalysisInput("piano", "22")]);
        var b = AnalysisRules.Fingerprint([new AnalysisInput("piano", "22"), new AnalysisInput("bass", "11")]);
        var c = AnalysisRules.Fingerprint([new AnalysisInput("bass", "11"), new AnalysisInput("piano", "23")]);

        a.ShouldBe(b);
        a.ShouldNotBe(c);
        a.Length.ShouldBe(TrackAnalysis.FingerprintLength);
        a.ShouldMatch("^[0-9a-f]{64}$");
        Should.Throw<ArgumentNullException>(() => AnalysisRules.Fingerprint(null!));
    }

    [Theory]
    [InlineData(300, 0.03, false, 14)]
    [InlineData(300, 0.03, true, 29)]
    [InlineData(200, 0.035, false, 12)]
    [InlineData(-5, -1, false, 5)]
    public void Estimates_AreDurationTimesFactorPlusOverhead(double duration, double factor, bool weightsMissing, int seconds) =>
        AnalysisRules.EstimateSeconds(duration, factor, weightsMissing).ShouldBe(seconds);
}
