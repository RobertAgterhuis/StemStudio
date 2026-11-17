using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Domain.Tests.Tracks;

/// <summary>013 T021: removed detail stems (data-model 5.3, FR-025).</summary>
public sealed class DetailFilesRemovedTests
{
    private static readonly DateTime Removed = DetailTestData.T0.AddHours(2);

    [Fact]
    public void RemoveDetailFiles_MarksACompletedPhaseAndKeepsTheNames()
    {
        var track = DetailTestData.DetailCompleted();

        track.RemoveDetailFiles(Removed);

        var phase = track.DetailPhase!;
        (phase.Status, phase.FilesRemovedAt, phase.UpdatedAt).ShouldBe((DetailPhaseStatus.Completed, Removed, Removed));
        phase.Parents.SelectMany(parent => parent.Stems.Select(stem => $"{parent.Parent}/{stem.Name}")).ShouldBe(
            DetailTestData.DetailCompleted().DetailPhase!.Parents.SelectMany(parent => parent.Stems.Select(stem => $"{parent.Parent}/{stem.Name}")));
        track.HasDetailFiles.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemoveDetailFiles_RefusesAPhaseThatIsNotCompleted(bool requested)
    {
        var track = requested ? DetailTestData.Requested() : DetailTestData.CompletedTrack();

        Should.Throw<InvalidOperationException>(() => track.RemoveDetailFiles(Removed));
    }

    [Fact]
    public void CompletedPhase_WithRemovedFiles_CanStartAgain_AndCompletionClearsTheMark()
    {
        var track = DetailTestData.DetailCompleted();
        track.CanStartDetail.ShouldBeFalse();
        track.HasDetailFiles.ShouldBeTrue();
        track.RemoveDetailFiles(Removed);

        track.CanStartDetail.ShouldBeTrue();
        track.RequestDetail(Removed.AddMinutes(1));
        track.DetailPhase!.FilesRemovedAt.ShouldBe(Removed);
        track.CompleteDetail(DetailTestData.Outcomes(), Removed.AddMinutes(5));

        (track.DetailPhase.Status, track.DetailPhase.FilesRemovedAt, track.HasDetailFiles).ShouldBe((DetailPhaseStatus.Completed, null, true));
    }

    [Fact]
    public void HasDetailFiles_IsFalseWithoutACompletedPhase() => DetailTestData.Requested().HasDetailFiles.ShouldBeFalse();
}

/// <summary>013 T050: a rehydrated worker job replaces the standard job of a completed track (FR-019).</summary>
public sealed class RehydratedWorkerJobTests
{
    [Fact]
    public void RehydrateWorkerJob_ReplacesTheJobOfACompletedTrack()
    {
        var track = DetailTestData.CompletedTrack();

        track.RehydrateWorkerJob("5d4c3b2a1f0e4d9c8b7a6f5e4d3c2b1a", DetailTestData.T0.AddDays(1));

        (track.WorkerJobId, track.UpdatedAt).ShouldBe(("5d4c3b2a1f0e4d9c8b7a6f5e4d3c2b1a", DetailTestData.T0.AddDays(1)));
    }

    [Fact]
    public void RehydrateWorkerJob_RefusesInvalidIdsAndTracksThatAreNotCompleted()
    {
        Should.Throw<ArgumentException>(() => DetailTestData.CompletedTrack().RehydrateWorkerJob("nope", DetailTestData.T0));
        var queued = StemStudio.Domain.Tracks.Track.FromUrl(Guid.NewGuid(), StemStudio.Domain.Tracks.SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/dQw4w9WgXcQ", DetailTestData.T0);
        Should.Throw<InvalidOperationException>(() => queued.RehydrateWorkerJob("5d4c3b2a1f0e4d9c8b7a6f5e4d3c2b1a", DetailTestData.T0));
    }
}

/// <summary>013 FR-019: the stage text of a queued detail phase while the worker cache is rehydrated.</summary>
public sealed class DetailPreparingStemsTests
{
    [Fact]
    public void MarkDetailPreparingStems_SetsTheStageOfAQueuedPhase()
    {
        var track = DetailTestData.Requested();
        var at = DetailTestData.T0.AddMinutes(5);

        track.MarkDetailPreparingStems(at);

        (track.DetailPhase!.Status, track.DetailPhase.Stage, track.DetailPhase.UpdatedAt).ShouldBe((DetailPhaseStatus.Queued, "Preparing stems", at));
    }

    [Fact]
    public void MarkDetailPreparingStems_IsIgnoredWithoutAnActivePhase()
    {
        var track = DetailTestData.DetailCompleted();
        var before = track.DetailPhase;

        track.MarkDetailPreparingStems(DetailTestData.T0.AddHours(3));

        track.DetailPhase.ShouldBe(before);
    }
}
