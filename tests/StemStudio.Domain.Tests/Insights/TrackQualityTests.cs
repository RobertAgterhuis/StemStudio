using StemStudio.Domain.Insights;
using StemStudio.Domain.Tests.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Insights;

/// <summary>Feature 020 T020: the quality report state machine on the track (data-model 5.1, 7).</summary>
public sealed class TrackQualityTests
{
    private const string JobId = "0d1e2f3a4b5c4d6e8f9a0b1c2d3e4f5a";
    private static readonly DateTime T0 = DetailTestData.T0;

    [Fact]
    public void ANewTrack_HasNoReport_AndCannotRequestOneBeforeItIsCompleted()
    {
        var queued = Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/dQw4w9WgXcQ", T0);

        (queued.QualityReport, queued.IsQualityActive).ShouldBe((null, false));
        Should.Throw<InvalidOperationException>(() => queued.RequestQuality("v1", T0));
    }

    [Fact]
    public void Request_QueuesARun_AndASecondRequestWhileActiveIsRefused()
    {
        var track = DetailTestData.CompletedTrack();

        track.RequestQuality("v1", T0.AddMinutes(1));

        var report = track.QualityReport!;
        (report.Status, report.Progress, report.RequestedStemsVersion, report.StemsVersion, report.Stems.Count).ShouldBe((QualityReportStatus.Queued, 0, "v1", (string?)null, 0));
        (report.RequestedAt, report.UpdatedAt, track.UpdatedAt, track.IsQualityActive).ShouldBe((T0.AddMinutes(1), T0.AddMinutes(1), T0.AddMinutes(1), true));
        Should.Throw<InvalidOperationException>(() => track.RequestQuality("v2", T0));
        Should.Throw<ArgumentException>(() => DetailTestData.CompletedTrack().RequestQuality(" ", T0));
    }

    [Fact]
    public void JobAndProgress_MoveTheRunForward_NeverBackwards()
    {
        var track = Requested();

        track.AssignQualityJob(JobId, T0.AddMinutes(2));
        track.ApplyQualityProgress(false, 0.1, T0.AddMinutes(3));
        (track.QualityReport!.Status, track.QualityReport.Progress, track.QualityReport.WorkerJobId).ShouldBe((QualityReportStatus.Queued, 10, JobId));
        track.ApplyQualityProgress(true, 0.555, T0.AddMinutes(4));
        track.ApplyQualityProgress(true, 0.2, T0.AddMinutes(5));
        track.ApplyQualityProgress(true, 7, T0.AddMinutes(6));

        (track.QualityReport.Status, track.QualityReport.Progress).ShouldBe((QualityReportStatus.Running, 100));
        Should.Throw<ArgumentException>(() => track.AssignQualityJob("nope", T0));
    }

    [Fact]
    public void Complete_ShowsTheNewStems_AndKeepsTheRatingsShownBeforeAsPrevious()
    {
        var track = Requested();
        track.CompleteQuality("1.0.0", [Stem("vocals", QualityRating.Poor), Stem("vocals", QualityRating.Good)], T0.AddMinutes(5));
        track.QualityReport!.Previous.ShouldBeEmpty();

        track.RequestQuality("v2", T0.AddMinutes(10));
        track.QualityReport.Stems.Count.ShouldBe(2, "the last ready stems stay readable while the new run is queued");
        track.CompleteQuality("1.1.0", [Stem("vocals", QualityRating.Fair)], T0.AddMinutes(12));

        var report = track.QualityReport;
        (report.Status, report.Progress, report.AnalyzerVersion, report.MeasuredAt, report.StemsVersion, report.Error).ShouldBe((QualityReportStatus.Ready, 100, "1.1.0", T0.AddMinutes(12), "v2", (string?)null));
        report.Stems.Select(stem => stem.Rating).ShouldBe([QualityRating.Fair]);
        report.Previous.ShouldBe(new Dictionary<string, QualityRating> { ["vocals"] = QualityRating.Poor });
        (track.IsQualityActive, report.WorkerJobId).ShouldBe((false, null));
    }

    [Fact]
    public void Fail_KeepsTheLastReadyStemsAndPrevious()
    {
        var track = Requested();
        track.CompleteQuality("1.0.0", [Stem("bass", QualityRating.Good)], T0.AddMinutes(5));
        track.RequestQuality("v2", T0.AddMinutes(6));
        track.AssignQualityJob(JobId, T0.AddMinutes(6));

        track.FailQuality("  The stems could not be read.  ", T0.AddMinutes(7));

        var report = track.QualityReport!;
        (report.Status, report.Error, report.WorkerJobId, report.StemsVersion, report.AnalyzerVersion).ShouldBe((QualityReportStatus.Failed, "The stems could not be read.", (string?)null, "v1", "1.0.0"));
        report.Stems.Single().Channel.ShouldBe("bass");
        track.RequestQuality("v3", T0.AddMinutes(8));
        track.QualityReport!.Status.ShouldBe(QualityReportStatus.Queued, "a failed report can be requested again");
    }

    [Fact]
    public void Fail_UsesTheDefaultText_AndCutsLongTexts()
    {
        var empty = Requested();
        var longer = Requested();

        empty.FailQuality(null, T0);
        longer.FailQuality(new string('x', 900), T0);

        empty.QualityReport!.Error.ShouldBe(Track.QualityFailedDefault);
        longer.QualityReport!.Error!.Length.ShouldBe(QualityReport.MaxErrorLength);
    }

    [Fact]
    public void FinishingWithoutAnActiveRun_IsRefused_AndProgressIsIgnored()
    {
        var track = DetailTestData.CompletedTrack();

        track.ApplyQualityProgress(true, 0.5, T0);

        track.QualityReport.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => track.CompleteQuality("1.0.0", [], T0));
        Should.Throw<InvalidOperationException>(() => track.FailQuality("x", T0));
        Should.Throw<InvalidOperationException>(() => track.AssignQualityJob(JobId, T0));
        Should.Throw<ArgumentException>(() => Requested().CompleteQuality(" ", [], T0));
    }

    private static Track Requested()
    {
        var track = DetailTestData.CompletedTrack();
        track.RequestQuality("v1", T0.AddMinutes(1));
        return track;
    }

    internal static StemQuality Stem(string channel, QualityRating rating) => new(
        channel,
        rating,
        QualityReason.Clean,
        100,
        0.5,
        -6,
        new BleedEstimate(QualityLevel.Low, 0.05, null, false, Reliability.Indicative),
        new ArtefactEstimate(QualityLevel.Low, 0.1, [], Reliability.Indicative));
}
