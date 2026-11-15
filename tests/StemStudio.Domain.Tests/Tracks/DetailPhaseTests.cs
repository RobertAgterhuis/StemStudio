using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;
using static StemStudio.Domain.Tests.Tracks.DetailTestData;

namespace StemStudio.Domain.Tests.Tracks;

/// <summary>Detail phase state machine (data-model.md 3.1 and 4.1; FR-001, FR-007, FR-008).</summary>
public sealed class DetailPhaseTests
{
    [Fact]
    public void NewTrack_HasNoDetailPhase()
    {
        var track = CompletedTrack();

        track.DetailPhase.ShouldBeNull();
        track.IsDetailActive.ShouldBeFalse();
        track.CanStartDetail.ShouldBeTrue();
    }

    [Fact]
    public void RequestDetail_QueuesAFreshPhaseWithoutTouchingTheStandardResult()
    {
        var track = CompletedTrack();
        var stems = track.Stems.ToList();

        track.RequestDetail(T0.AddMinutes(1));

        var phase = track.DetailPhase!;
        phase.Status.ShouldBe(DetailPhaseStatus.Queued);
        phase.Stage.ShouldBe("Queued");
        (phase.Progress, phase.Error, phase.WorkerDetailJobId, phase.CurrentParent).ShouldBe((0, null, null, null));
        phase.Parents.ShouldBeEmpty();
        (phase.RequestedAt, phase.UpdatedAt, track.UpdatedAt).ShouldBe((T0.AddMinutes(1), T0.AddMinutes(1), T0.AddMinutes(1)));
        AssertStandardResultUnchanged(track, stems);
        track.IsDetailActive.ShouldBeTrue();
    }

    [Fact]
    public void RequestDetail_RequiresACompletedTrack()
    {
        var queued = Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/dQw4w9WgXcQ", T0);
        var failed = Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:aaaaaaaaaaa"), "https://youtu.be/aaaaaaaaaaa", T0);
        failed.Fail("boom", T0);

        queued.CanStartDetail.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => queued.RequestDetail(T0));
        Should.Throw<InvalidOperationException>(() => failed.RequestDetail(T0));
    }

    [Theory]
    [InlineData(DetailPhaseStatus.Queued)]
    [InlineData(DetailPhaseStatus.Running)]
    [InlineData(DetailPhaseStatus.Completed)]
    public void RequestDetail_IsRejectedWhileActiveOrCompleted(DetailPhaseStatus status)
    {
        var track = InStatus(status);

        track.CanStartDetail.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => track.RequestDetail(T0.AddHours(1)));
    }

    [Theory]
    [InlineData(DetailPhaseStatus.Failed)]
    [InlineData(DetailPhaseStatus.Cancelled)]
    public void RequestDetail_RestartsAFailedOrCancelledPhase(DetailPhaseStatus status)
    {
        var track = InStatus(status);

        track.CanStartDetail.ShouldBeTrue();
        track.RequestDetail(T0.AddHours(1));

        var phase = track.DetailPhase!;
        (phase.Status, phase.Progress, phase.Error, phase.WorkerDetailJobId).ShouldBe((DetailPhaseStatus.Queued, 0, null, null));
        phase.RequestedAt.ShouldBe(T0.AddHours(1));
    }

    [Fact]
    public void AssignDetailJob_StoresTheWorkerJob()
    {
        var track = Requested();

        track.AssignDetailJob(DetailJobId, T0.AddMinutes(2));

        track.DetailPhase!.WorkerDetailJobId.ShouldBe(DetailJobId);
        track.DetailPhase.UpdatedAt.ShouldBe(T0.AddMinutes(2));
    }

    [Theory]
    [InlineData("not-hex")]
    [InlineData("7C1E9A2B4D6F48E0A3B5C7D9E1F20A4B")]
    public void AssignDetailJob_RejectsInvalidIds(string id)
    {
        Should.Throw<ArgumentException>(() => Requested().AssignDetailJob(id, T0));
    }

    [Fact]
    public void AssignDetailJob_RequiresAnActivePhase()
    {
        Should.Throw<InvalidOperationException>(() => CompletedTrack().AssignDetailJob(DetailJobId, T0));
        Should.Throw<InvalidOperationException>(() => DetailCompleted().AssignDetailJob(DetailJobId, T0));
    }

    [Fact]
    public void ApplyDetailProgress_CopiesRunningState()
    {
        var track = Requested();

        track.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "Splitting drums (1 of 3)", "drums", 12), T0.AddMinutes(3));

        var phase = track.DetailPhase!;
        (phase.Status, phase.Stage, phase.CurrentParent, phase.Progress).ShouldBe((DetailPhaseStatus.Running, "Splitting drums (1 of 3)", "drums", 12));
        (phase.UpdatedAt, track.UpdatedAt).ShouldBe((T0.AddMinutes(3), T0.AddMinutes(3)));
        track.Status.ShouldBe(TrackStatus.Completed);
    }

    [Fact]
    public void ApplyDetailProgress_NeverDecreasesProgressAndIgnoresBackwardStates()
    {
        var track = Requested();
        track.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "Splitting vocals (2 of 3)", "vocals", 40), T0);

        track.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "Splitting vocals (2 of 3)", "vocals", 30), T0);
        track.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Queued, "Queued", null, 0), T0);

        var phase = track.DetailPhase!;
        (phase.Status, phase.Progress, phase.CurrentParent).ShouldBe((DetailPhaseStatus.Running, 40, "vocals"));
    }

    [Fact]
    public void ApplyDetailProgress_ClampsProgressCutsTheStageAndDropsUnknownParents()
    {
        var track = Requested();

        track.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, new string('s', 300), "bass", 150), T0);

        var phase = track.DetailPhase!;
        (phase.Progress, phase.Stage!.Length, phase.CurrentParent).ShouldBe((100, Track.MaxStageLength, null));
    }

    [Theory]
    [InlineData(DetailPhaseStatus.Completed)]
    [InlineData(DetailPhaseStatus.Failed)]
    [InlineData(DetailPhaseStatus.Cancelled)]
    public void ApplyDetailProgress_RejectsTerminalReports(DetailPhaseStatus status)
    {
        Should.Throw<ArgumentException>(() => Requested().ApplyDetailProgress(new DetailProgress(status, null, null, 0), T0));
    }

    [Fact]
    public void ApplyDetailProgress_IsIgnoredWithoutAnActivePhase()
    {
        var none = CompletedTrack();
        var done = DetailCompleted();
        var report = new DetailProgress(DetailPhaseStatus.Running, "x", "drums", 5);

        none.ApplyDetailProgress(report, T0.AddHours(2));
        done.ApplyDetailProgress(report, T0.AddHours(2));

        none.DetailPhase.ShouldBeNull();
        done.DetailPhase!.Status.ShouldBe(DetailPhaseStatus.Completed);
        done.UpdatedAt.ShouldNotBe(T0.AddHours(2));
    }

    [Fact]
    public void CompleteDetail_StoresOrderedOutcomesAndForces100()
    {
        var track = Requested();
        track.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "x", "drums", 60), T0);
        var stems = track.Stems.ToList();
        var shuffled = Outcomes().AsEnumerable().Reverse().ToList();
        shuffled[4] = Split("drums", "rest", "snare", "kick");

        track.CompleteDetail(shuffled, T0.AddMinutes(30));

        var phase = track.DetailPhase!;
        (phase.Status, phase.Stage, phase.Progress, phase.CurrentParent, phase.Error).ShouldBe((DetailPhaseStatus.Completed, "Completed", 100, null, null));
        phase.Parents.Select(p => p.Parent).ShouldBe(StemCatalog.SixStem);
        phase.Parents[1].Stems.Select(s => s.Name).ShouldBe(["kick", "snare", "rest"]);
        (phase.UpdatedAt, phase.RequestedAt).ShouldBe((T0.AddMinutes(30), T0.AddMinutes(1)));
        AssertStandardResultUnchanged(track, stems);
        track.IsDetailActive.ShouldBeFalse();
    }

    public static TheoryData<string, ParentOutcome, string> InvalidOutcomes() => new()
    {
        { "parent that is not a stem of the track", Split("strings", "brass", "keys", "rest"), "parents[1].parent" },
        { "bass split", Split("bass", "kick", "snare", "rest"), "parents[1].stems" },
        { "split with one kept stem", Split("drums", "kick", "rest"), "parents[1].stems" },
        { "split without rest", Split("drums", "kick", "snare"), "parents[1].stems" },
        { "stem of another parent", Split("drums", "kick", "lead", "rest"), "parents[1].stems" },
        { "duplicate stem", Split("drums", "kick", "kick", "rest"), "parents[1].stems" },
        { "unsplit with stems", new ParentOutcome("drums", ParentOutcomeKind.NothingFurther, [new DetailStem("kick", 1)]), "parents[1].stems" },
        { "negative size", new ParentOutcome("drums", ParentOutcomeKind.Split, [new("kick", 1), new("snare", -1), new("rest", 1)]), "parents[1].stems" },
        { "duplicate parent", Unsplit("vocals", ParentOutcomeKind.SkippedSilent), "parents[1].parent" },
    };

    [Theory]
    [MemberData(nameof(InvalidOutcomes))]
    public void CompleteDetail_RejectsInvalidOutcomes(string because, ParentOutcome invalid, string field)
    {
        var track = Requested();
        var outcomes = Outcomes();
        outcomes[1] = invalid;

        var error = Should.Throw<DomainValidationException>(() => track.CompleteDetail(outcomes, T0), because);

        error.Errors.ShouldContain(e => e.Field == field, because);
        track.DetailPhase!.Status.ShouldBe(DetailPhaseStatus.Queued);
    }

    [Fact]
    public void CompleteDetail_RejectsTooManyStems()
    {
        var instruments = DetailCatalog.AllowedFor("other").Concat(["rest", "rest"]).ToArray();
        var outcomes = Outcomes();
        outcomes[5] = Split("other", instruments);

        var error = Should.Throw<DomainValidationException>(() => Requested().CompleteDetail(outcomes, T0));

        error.Errors.ShouldContain(e => e.Field == "parents[5].stems");
    }

    [Fact]
    public void CompleteDetail_RequiresAnActivePhase()
    {
        Should.Throw<InvalidOperationException>(() => CompletedTrack().CompleteDetail(Outcomes(), T0));
        Should.Throw<InvalidOperationException>(() => DetailCompleted().CompleteDetail(Outcomes(), T0));
    }

    [Fact]
    public void FailDetail_KeepsProgressAndTheStandardResult()
    {
        var track = Requested();
        track.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "x", "drums", 33), T0);
        var stems = track.Stems.ToList();

        track.FailDetail("interrupted by restart", T0.AddMinutes(5));

        var phase = track.DetailPhase!;
        (phase.Status, phase.Stage, phase.Progress, phase.Error, phase.CurrentParent).ShouldBe((DetailPhaseStatus.Failed, "Failed", 33, "interrupted by restart", null));
        phase.Parents.ShouldBeEmpty();
        AssertStandardResultUnchanged(track, stems);
    }

    [Fact]
    public void FailDetail_UsesADefaultForBlankAndCutsTo1000()
    {
        var blank = Requested();
        var longError = Requested();

        blank.FailDetail("  ", T0);
        longError.FailDetail(new string('e', 1500), T0);

        blank.DetailPhase!.Error.ShouldBe(Track.DetailFailedDefault);
        longError.DetailPhase!.Error!.Length.ShouldBe(Track.MaxErrorLength);
    }

    [Theory]
    [InlineData(DetailPhaseStatus.Queued)]
    [InlineData(DetailPhaseStatus.Running)]
    public void CancelDetail_FromQueuedOrRunning(DetailPhaseStatus status)
    {
        var track = InStatus(status);

        track.CancelDetail(T0.AddHours(1));

        var phase = track.DetailPhase!;
        (phase.Status, phase.Stage, phase.Error, phase.CurrentParent).ShouldBe((DetailPhaseStatus.Cancelled, "Cancelled", null, null));
        phase.UpdatedAt.ShouldBe(T0.AddHours(1));
    }

    [Theory]
    [InlineData(DetailPhaseStatus.Completed)]
    [InlineData(DetailPhaseStatus.Failed)]
    [InlineData(DetailPhaseStatus.Cancelled)]
    public void TerminalPhases_CannotBeCancelledOrFailed(DetailPhaseStatus status)
    {
        var track = InStatus(status);

        Should.Throw<InvalidOperationException>(() => track.CancelDetail(T0));
        Should.Throw<InvalidOperationException>(() => track.FailDetail("x", T0));
        Should.Throw<InvalidOperationException>(() => CompletedTrack().CancelDetail(T0));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(DetailPhaseStatus.Queued, false)]
    [InlineData(DetailPhaseStatus.Running, false)]
    [InlineData(DetailPhaseStatus.Completed, true)]
    [InlineData(DetailPhaseStatus.Failed, true)]
    [InlineData(DetailPhaseStatus.Cancelled, true)]
    public void CanDelete_IsRefusedWhileTheDetailPhaseIsActive(DetailPhaseStatus? status, bool expected)
    {
        var track = status is { } value ? InStatus(value) : CompletedTrack();

        track.CanDelete.ShouldBe(expected);
    }

    [Fact]
    public void CanDelete_IsRefusedWhileTheStandardJobRuns()
    {
        Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/x", T0).CanDelete.ShouldBeFalse();
    }

    [Fact]
    public void DetailCatalog_ListsParentsAndAllowedNames()
    {
        DetailCatalog.Parents.ShouldBe(["vocals", "drums", "guitar", "piano", "other"]);
        DetailCatalog.AllowedFor("drums").ShouldBe(["kick", "snare", "toms", "hihat", "cymbals", "rest"]);
        DetailCatalog.AllowedFor("vocals").ShouldBe(["lead", "backing", "rest"]);
        DetailCatalog.AllowedFor("piano")[^1].ShouldBe("rest");
        DetailCatalog.AllowedFor("bass").ShouldBeEmpty();
        DetailCatalog.IsAllowed("guitar", "acoustic-guitar").ShouldBeTrue();
        DetailCatalog.IsAllowed("vocals", "kick").ShouldBeFalse();
        DetailCatalog.IsParent("other").ShouldBeTrue();
        DetailCatalog.IsParent("bass").ShouldBeFalse();
    }

    private static Track InStatus(DetailPhaseStatus status)
    {
        var track = Requested();
        Action<Track> move = status switch
        {
            DetailPhaseStatus.Running => t => t.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "Splitting drums (1 of 3)", "drums", 10), T0.AddMinutes(2)),
            DetailPhaseStatus.Completed => t => t.CompleteDetail(Outcomes(), T0.AddMinutes(2)),
            DetailPhaseStatus.Failed => t => t.FailDetail("boom", T0.AddMinutes(2)),
            DetailPhaseStatus.Cancelled => t => t.CancelDetail(T0.AddMinutes(2)),
            _ => _ => { },
        };
        move(track);
        return track;
    }

    private static void AssertStandardResultUnchanged(Track track, List<Stem> stems)
    {
        track.Status.ShouldBe(TrackStatus.Completed);
        track.Progress.ShouldBe(100);
        track.Error.ShouldBeNull();
        track.Stems.ShouldBe(stems);
    }
}
