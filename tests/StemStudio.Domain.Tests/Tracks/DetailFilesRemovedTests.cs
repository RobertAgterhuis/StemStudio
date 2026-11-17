using StemStudio.Domain.Tracks;
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
        phase.Parents.ShouldBe(DetailTestData.DetailCompleted().DetailPhase!.Parents, ignoreOrder: false);
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
