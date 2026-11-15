using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using static StemStudio.Domain.Tests.Lyrics.LyricsTestData;

namespace StemStudio.Domain.Tests.Lyrics;

/// <summary>Feature 006 T019: the lyrics state machine of a track (data-model 3.2, FR-002, FR-004, FR-007, FR-009, FR-013).</summary>
public sealed class TrackLyricsTests
{
    private static readonly DateTime T1 = T0.AddMinutes(1);

    [Fact]
    public void ANewTrack_HasNoLyricsAndStatusNone()
    {
        var track = CompletedTrack();

        (track.LyricsStatus, track.Lyrics).ShouldBe((LyricsStatus.None, null));
    }

    [Fact]
    public void EnsureLyrics_CreatesTheRowOnceForTheTrack()
    {
        var track = CompletedTrack();

        var lyrics = track.EnsureLyrics(T1);

        (lyrics.TrackId, lyrics.Status, lyrics.OffsetMs, lyrics.UpdatedAt).ShouldBe((track.Id, LyricsStatus.None, 0, T1));
        track.EnsureLyrics(T1.AddMinutes(1)).ShouldBeSameAs(lyrics);
    }

    [Fact]
    public void TheTrackStatus_FollowsTheLoadedLyrics()
    {
        var track = TrackWith(LyricsStatus.Searching);

        track.Lyrics!.ApplyLookup(LyricsOutcome.Found, Word(), T1);

        track.LyricsStatus.ShouldBe(LyricsStatus.Found);
    }

    [Fact]
    public void ScheduleLyricsLookup_StartsTheAutomaticSearchOnceTheDurationIsKnown()
    {
        var track = CompletedTrack();

        track.ScheduleLyricsLookup(T1);

        track.LyricsStatus.ShouldBe(LyricsStatus.Searching);
        (track.Lyrics!.LyricsJobId, track.Lyrics.UpdatedAt).ShouldBe((null, T1));
    }

    [Fact]
    public void ScheduleLyricsLookup_WithoutADurationDoesNothing()
    {
        var track = Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/dQw4w9WgXcQ", T0);

        track.ScheduleLyricsLookup(T1);

        (track.LyricsStatus, track.Lyrics).ShouldBe((LyricsStatus.None, null));
    }

    [Fact]
    public void ScheduleLyricsLookup_UsesTheLookupDurationBeforeCompletion()
    {
        var track = Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/dQw4w9WgXcQ", T0);
        track.ScheduleLookup(new LookupQuery("Song", "Band", null, 200), T0);

        track.ScheduleLyricsLookup(T1);

        (track.LyricsDurationSeconds, track.LyricsStatus).ShouldBe((200, LyricsStatus.Searching));
    }

    [Theory]
    [InlineData(LyricsStatus.Removed)]
    [InlineData(LyricsStatus.Found)]
    [InlineData(LyricsStatus.NotFound)]
    public void ScheduleAutomatic_NeverRestartsAfterTheFirstLookup(LyricsStatus status)
    {
        var track = TrackWith(status);

        track.ScheduleLyricsLookup(T1);
        track.Lyrics!.ScheduleAutomatic(T1);

        track.LyricsStatus.ShouldBe(status);
    }

    [Fact]
    public void EnsureLyrics_RefusesWhenTheRowWasNotLoaded()
    {
        // As materialised by EF Core without the TrackLyrics row: only the stored Tracks.LyricsStatus column is set.
        var track = CompletedTrack();
        typeof(Track).GetProperty(nameof(Track.LyricsStatus))!.SetValue(track, LyricsStatus.Found);

        track.LyricsStatus.ShouldBe(LyricsStatus.Found);
        Should.Throw<InvalidOperationException>(() => track.EnsureLyrics(T1)).Message.ShouldContain("not loaded");
    }

    [Fact]
    public void AssignJob_RecordsTheWorkerJobOfASearchOrAlignment()
    {
        var searching = TrackWith(LyricsStatus.Searching).Lyrics!;
        var aligning = TrackWith(LyricsStatus.Aligning).Lyrics!;

        searching.AssignJob(JobId, T1);
        aligning.AssignJob(JobId, T1);

        (searching.LyricsJobId, aligning.LyricsJobId, aligning.UpdatedAt).ShouldBe((JobId, JobId, T1));
        Should.Throw<InvalidOperationException>(() => searching.AssignJob(JobId, T1));
        Should.Throw<ArgumentException>(() => TrackWith(LyricsStatus.Searching).Lyrics!.AssignJob("ABC", T1));
        Should.Throw<InvalidOperationException>(() => TrackWith(LyricsStatus.Found).Lyrics!.AssignJob(JobId, T1));
    }

    [Theory]
    [InlineData(SyncLevel.Word)]
    [InlineData(SyncLevel.Line)]
    public void ATimedLookupResult_IsFound(SyncLevel level)
    {
        var lyrics = Searching();

        lyrics.ApplyLookup(LyricsOutcome.Found, level == SyncLevel.Word ? Word() : Line(), T1);

        (lyrics.Status, lyrics.Document!.SyncLevel, lyrics.LyricsJobId, lyrics.Reason, lyrics.AutoAlign).ShouldBe((LyricsStatus.Found, level, null, null, false));
    }

    [Fact]
    public void APlainLookupResult_WaitsForTheAlignmentAndAsksForTheAutomaticRule()
    {
        var lyrics = Searching();

        lyrics.ApplyLookup(LyricsOutcome.Found, Plain(), T1);

        (lyrics.Status, lyrics.AutoAlign, lyrics.HasText).ShouldBe((LyricsStatus.AlignPending, true, true));
    }

    [Fact]
    public void AnInstrumentalResult_KeepsTheEmptyDocument()
    {
        var lyrics = Searching();

        lyrics.ApplyLookup(LyricsOutcome.Instrumental, Instrumental(), T1);

        (lyrics.Status, lyrics.Document!.Instrumental, lyrics.HasText).ShouldBe((LyricsStatus.Instrumental, true, false));
    }

    [Theory]
    [InlineData(LyricsOutcome.NotFound, LyricsStatus.NotFound, TrackLyrics.NotFoundReason)]
    [InlineData(LyricsOutcome.Disabled, LyricsStatus.Disabled, TrackLyrics.DisabledReason)]
    public void ResultsWithoutLyrics_ClearTheDocumentAndExplain(LyricsOutcome outcome, LyricsStatus status, string reason)
    {
        var lyrics = TrackWith(LyricsStatus.Found).Lyrics!;
        lyrics.Search(T1);

        lyrics.ApplyLookup(outcome, null, T1);

        (lyrics.Status, lyrics.Document, lyrics.Reason).ShouldBe((status, null, reason));
    }

    [Fact]
    public void InvalidWorkerData_FailsWithAReadableReasonWithoutTheText()
    {
        var lyrics = Searching();
        var broken = Word() with { Lines = [new LyricsLine(5000, 4000, "Fake line one", [new LyricsWord("Fake", 5000, 4000, WordOrigin.Source, false)])] };

        lyrics.ApplyLookup(LyricsOutcome.Found, broken, T1);

        lyrics.Status.ShouldBe(LyricsStatus.Failed);
        lyrics.Reason.ShouldStartWith(TrackLyrics.UnreadableReason);
        lyrics.Reason.ShouldNotContain("Fake");
        lyrics.Document.ShouldBeNull();
    }

    [Fact]
    public void AFoundOutcomeWithoutADocument_FailsAsUnreadable()
    {
        var lyrics = Searching();

        lyrics.ApplyLookup(LyricsOutcome.Found, null, T1);

        (lyrics.Status, lyrics.Reason).ShouldBe((LyricsStatus.Failed, TrackLyrics.UnreadableReason));
    }

    [Fact]
    public void ApplyLookup_OnlyWhileSearching()
    {
        Should.Throw<InvalidOperationException>(() => TrackWith(LyricsStatus.Found).Lyrics!.ApplyLookup(LyricsOutcome.NotFound, null, T1));
    }

    [Theory]
    [InlineData(LyricsStatus.None)]
    [InlineData(LyricsStatus.Found)]
    [InlineData(LyricsStatus.Instrumental)]
    [InlineData(LyricsStatus.NotFound)]
    [InlineData(LyricsStatus.AlignPending)]
    [InlineData(LyricsStatus.Failed)]
    [InlineData(LyricsStatus.Disabled)]
    [InlineData(LyricsStatus.Removed)]
    public void Paste_FromEveryIdleStateBecomesAPlainUserDocumentWaitingForTheAlignment(LyricsStatus status)
    {
        var lyrics = TrackWith(status).Lyrics!;

        lyrics.Paste(LyricsDocument.FromUserText("Fake line one\nLa la la", "en"), T1);

        (lyrics.Status, lyrics.Document!.Source, lyrics.Document.SyncLevel, lyrics.AutoAlign, lyrics.Reason).ShouldBe((LyricsStatus.AlignPending, LyricsSource.User, SyncLevel.Plain, true, null));
    }

    [Theory]
    [InlineData(LyricsStatus.Searching)]
    [InlineData(LyricsStatus.Aligning)]
    public void Paste_SearchAndOffset_AreBusyWhileTheWorkerRuns(LyricsStatus status)
    {
        var lyrics = TrackWith(status).Lyrics!;

        Conflict(() => lyrics.Paste(Plain(LyricsSource.User), T1)).ShouldBe(LyricsConflict.Busy);
        Conflict(() => lyrics.Search(T1)).ShouldBe(LyricsConflict.Busy);
    }

    [Theory]
    [InlineData(LyricsStatus.None)]
    [InlineData(LyricsStatus.Found)]
    [InlineData(LyricsStatus.Instrumental)]
    [InlineData(LyricsStatus.NotFound)]
    [InlineData(LyricsStatus.AlignPending)]
    [InlineData(LyricsStatus.Failed)]
    [InlineData(LyricsStatus.Disabled)]
    [InlineData(LyricsStatus.Removed)]
    public void Search_StartsANewLookupAndKeepsTheTextUntilTheResult(LyricsStatus status)
    {
        var lyrics = TrackWith(status).Lyrics!;
        var before = lyrics.Document;

        lyrics.Search(T1);

        (lyrics.Status, lyrics.LyricsJobId, lyrics.Reason, lyrics.Document, lyrics.AutoAlign).ShouldBe((LyricsStatus.Searching, null, null, before, false));
    }

    [Theory]
    [InlineData(LyricsStatus.AlignPending, SyncLevel.Plain)]
    [InlineData(LyricsStatus.Failed, SyncLevel.Plain)]
    public void StartAlign_FromTextWithoutTiming(LyricsStatus status, SyncLevel level)
    {
        var lyrics = TrackWith(status).Lyrics!;

        lyrics.StartAlign(T1);

        (lyrics.Status, lyrics.Document!.SyncLevel, lyrics.LyricsJobId, lyrics.AlignProgress, lyrics.AutoAlign, lyrics.IsRefining).ShouldBe((LyricsStatus.Aligning, level, null, 0, false, false));
    }

    [Fact]
    public void StartAlign_RefinesALineSyncedDocument()
    {
        var lyrics = Searching();
        lyrics.ApplyLookup(LyricsOutcome.Found, Line(), T0);

        lyrics.StartAlign(T1);

        (lyrics.Status, lyrics.IsRefining).ShouldBe((LyricsStatus.Aligning, true));
    }

    [Theory]
    [InlineData(LyricsStatus.None, LyricsConflict.NoText)]
    [InlineData(LyricsStatus.NotFound, LyricsConflict.NoText)]
    [InlineData(LyricsStatus.Instrumental, LyricsConflict.NoText)]
    [InlineData(LyricsStatus.Removed, LyricsConflict.NoText)]
    [InlineData(LyricsStatus.Found, LyricsConflict.AlreadyTimed)]
    [InlineData(LyricsStatus.Aligning, LyricsConflict.Busy)]
    [InlineData(LyricsStatus.Searching, LyricsConflict.Busy)]
    public void StartAlign_ConflictsWithoutAlignableText(LyricsStatus status, LyricsConflict conflict)
    {
        var lyrics = TrackWith(status).Lyrics!;

        Conflict(() => lyrics.StartAlign(T1)).ShouldBe(conflict);
    }

    [Fact]
    public void ApplyAlignment_RecordsProgressDeviceAndEstimate()
    {
        var lyrics = TrackWith(LyricsStatus.Aligning).Lyrics!;
        var gpu = new JobDevice(ProcessingDeviceKind.Gpu, "NVIDIA GeForce RTX 5070 Ti", false, null);

        lyrics.ApplyAlignment(new AlignmentReport(40, gpu, 30), T1);
        lyrics.ApplyAlignment(new AlignmentReport(30, null, null), T1);

        (lyrics.AlignProgress, lyrics.AlignDevice, lyrics.AlignEstimateSeconds).ShouldBe((40, gpu, 30));
        lyrics.ApplyAlignment(new AlignmentReport(250, null, null), T1);
        lyrics.AlignProgress.ShouldBe(100);
        Should.Throw<InvalidOperationException>(() => TrackWith(LyricsStatus.Found).Lyrics!.ApplyAlignment(new AlignmentReport(1, null, null), T1));
    }

    [Fact]
    public void CompleteAlign_IsFoundWithAlignedTiming()
    {
        var lyrics = TrackWith(LyricsStatus.Aligning).Lyrics!;
        lyrics.AssignJob(JobId, T0);

        lyrics.CompleteAlign(Aligned(), T1);

        (lyrics.Status, lyrics.Document!.Origin, lyrics.LyricsJobId, lyrics.AlignProgress).ShouldBe((LyricsStatus.Found, LyricsOrigin.Aligned, null, null));
    }

    [Fact]
    public void CompleteAlign_WithInvalidOrPlainDataFails()
    {
        var invalid = TrackWith(LyricsStatus.Aligning).Lyrics!;
        var plain = TrackWith(LyricsStatus.Aligning).Lyrics!;

        invalid.CompleteAlign(Aligned() with { Language = "English" }, T1);
        plain.CompleteAlign(Plain(), T1);

        (invalid.Status, plain.Status).ShouldBe((LyricsStatus.Failed, LyricsStatus.Failed));
        invalid.Reason.ShouldStartWith(TrackLyrics.UnalignableReason);
        invalid.Document!.SyncLevel.ShouldBe(SyncLevel.Plain);
        Should.Throw<InvalidOperationException>(() => TrackWith(LyricsStatus.Found).Lyrics!.CompleteAlign(Aligned(), T1));
    }

    [Theory]
    [InlineData(LyricsStatus.Searching)]
    [InlineData(LyricsStatus.Aligning)]
    public void Fail_KeepsTheTextAndClearsTheJob(LyricsStatus status)
    {
        var lyrics = TrackWith(status).Lyrics!;
        lyrics.AssignJob(JobId, T0);

        lyrics.Fail("Lyrics could not be retrieved; try again later.", T1);

        (lyrics.Status, lyrics.Reason, lyrics.LyricsJobId).ShouldBe((LyricsStatus.Failed, "Lyrics could not be retrieved; try again later.", null));
    }

    [Fact]
    public void Fail_ABlankReasonUsesTheDefaultAndLongReasonsAreCut()
    {
        var blank = Searching();
        var longer = Searching();

        blank.Fail(" ", T1);
        longer.Fail(new string('x', 1500), T1);

        blank.Reason.ShouldBe(TrackLyrics.FailedDefault);
        longer.Reason!.Length.ShouldBe(1000);
        Should.Throw<InvalidOperationException>(() => TrackWith(LyricsStatus.Found).Lyrics!.Fail("x", T1));
    }

    [Fact]
    public void CancelAlign_GoesBackToAlignPendingOrToTheLineSyncedLyrics()
    {
        var plain = TrackWith(LyricsStatus.Aligning).Lyrics!;
        var refined = Searching();
        refined.ApplyLookup(LyricsOutcome.Found, Line(), T0);
        refined.StartAlign(T0);

        plain.CancelAlign(T1);
        refined.CancelAlign(T1);

        (plain.Status, plain.AutoAlign, plain.AlignProgress, refined.Status).ShouldBe((LyricsStatus.AlignPending, false, null, LyricsStatus.Found));
        Conflict(() => TrackWith(LyricsStatus.Found).Lyrics!.CancelAlign(T1)).ShouldBe(LyricsConflict.NotAligning);
    }

    [Theory]
    [InlineData(LyricsStatus.None)]
    [InlineData(LyricsStatus.Searching)]
    [InlineData(LyricsStatus.Found)]
    [InlineData(LyricsStatus.Aligning)]
    [InlineData(LyricsStatus.Failed)]
    public void Remove_ClearsEverythingAndBlocksTheAutomaticLookup(LyricsStatus status)
    {
        var track = TrackWith(status);
        if (status == LyricsStatus.Found)
        {
            track.Lyrics!.SetOffset(120, T0);
        }

        track.Lyrics!.Remove(T1);
        track.ScheduleLyricsLookup(T1);

        var lyrics = track.Lyrics;
        (lyrics.Status, lyrics.Document, lyrics.LyricsJobId, lyrics.Reason, lyrics.AlignEstimateSeconds, lyrics.AutoAlign).ShouldBe((LyricsStatus.Removed, null, null, null, null, false));
    }

    [Theory]
    [InlineData(-5000)]
    [InlineData(0)]
    [InlineData(120)]
    [InlineData(5000)]
    public void SetOffset_StoresValidOffsets(int offset)
    {
        var lyrics = TrackWith(LyricsStatus.Found).Lyrics!;

        lyrics.SetOffset(offset, T1);

        (lyrics.OffsetMs, lyrics.UpdatedAt).ShouldBe((offset, T1));
    }

    [Theory]
    [InlineData(-5010)]
    [InlineData(5010)]
    [InlineData(125)]
    public void SetOffset_RejectsValuesOutsideTheLimitsOrSteps(int offset)
    {
        var lyrics = TrackWith(LyricsStatus.Found).Lyrics!;

        var error = Should.Throw<DomainValidationException>(() => lyrics.SetOffset(offset, T1));

        error.Errors.Single().Field.ShouldBe("offsetMs");
    }

    [Theory]
    [InlineData(LyricsStatus.NotFound)]
    [InlineData(LyricsStatus.Instrumental)]
    [InlineData(LyricsStatus.Removed)]
    [InlineData(LyricsStatus.None)]
    public void SetOffset_NeedsLyrics(LyricsStatus status)
    {
        var lyrics = TrackWith(status).Lyrics!;

        Conflict(() => lyrics.SetOffset(100, T1)).ShouldBe(LyricsConflict.None);
    }

    [Fact]
    public void DeclineAutoAlign_StoresTheEstimateAndItsReason()
    {
        var lyrics = TrackWith(LyricsStatus.AlignPending).Lyrics!;

        lyrics.DeclineAutoAlign(420, "Aligning takes about 7 minutes on the CPU.", T1);

        (lyrics.Status, lyrics.AutoAlign, lyrics.AlignEstimateSeconds, lyrics.Reason).ShouldBe((LyricsStatus.AlignPending, false, 420, "Aligning takes about 7 minutes on the CPU."));
        Should.Throw<InvalidOperationException>(() => TrackWith(LyricsStatus.Found).Lyrics!.DeclineAutoAlign(1, "x", T1));
    }

    [Fact]
    public void LyricsConflictException_CarriesTheConflict()
    {
        var error = new LyricsConflictException(LyricsConflict.NoText);

        (error.Conflict, error.Message.Length > 0).ShouldBe((LyricsConflict.NoText, true));
        new LyricsConflictException().Conflict.ShouldBe(LyricsConflict.Busy);
        new LyricsConflictException("x").Message.ShouldBe("x");
        new LyricsConflictException("x", new InvalidOperationException()).InnerException.ShouldNotBeNull();
    }

    private static TrackLyrics Searching() => TrackWith(LyricsStatus.Searching).Lyrics!;

    private static LyricsConflict Conflict(Action action) => Should.Throw<LyricsConflictException>(action).Conflict;
}
