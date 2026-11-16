using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Tracks;

public sealed class TrackTests
{
    private const string JobId = "3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c";
    private static readonly DateTime T0 = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly SourceKey Youtube = SourceKey.Parse("https://youtu.be/dQw4w9WgXcQ").Key!;
    private static readonly SourceKey Upload = SourceKey.ForUpload(new string('a', 64));

    [Fact]
    public void FromUrl_StartsQueuedWithUrlAsTitle()
    {
        var track = NewTrack();

        track.Status.ShouldBe(TrackStatus.Queued);
        track.Stage.ShouldBe("Queued");
        track.Progress.ShouldBe(0);
        track.Title.ShouldBe("https://youtu.be/dQw4w9WgXcQ");
        track.SourceUrl.ShouldBe("https://youtu.be/dQw4w9WgXcQ");
        track.SourceType.ShouldBe(SourceType.Youtube);
        track.Key.ShouldBe(Youtube);
        track.Stems.ShouldBeEmpty();
        track.BeatGrid.ShouldBeNull();
        track.CreatedAt.ShouldBe(T0);
        track.UpdatedAt.ShouldBe(T0);
        track.IsTerminal.ShouldBeFalse();
    }

    [Fact]
    public void FromUrl_TruncatesLongUrlTitleTo500()
    {
        var url = "https://youtu.be/dQw4w9WgXcQ?x=" + new string('a', 900);

        Track.FromUrl(Guid.NewGuid(), Youtube, url, T0).Title.Length.ShouldBe(Track.MaxTitleLength);
    }

    [Fact]
    public void FromUrl_RejectsUploadKey()
    {
        Should.Throw<ArgumentException>(() => Track.FromUrl(Guid.NewGuid(), Upload, "x", T0));
    }

    [Theory]
    [InlineData("My Song.mp3", "My Song")]
    [InlineData(".flac", "Untitled upload")]
    [InlineData("  spaced  .wav", "spaced")]
    public void FromUpload_UsesFileNameWithoutExtensionAsTitle(string fileName, string title)
    {
        var track = Track.FromUpload(Guid.NewGuid(), Upload, new UploadedFile(fileName, "stored.mp3"), T0);

        track.Title.ShouldBe(title);
        track.SourceType.ShouldBe(SourceType.Upload);
        track.SourceUrl.ShouldBeNull();
        track.UploadFileName.ShouldBe("stored.mp3");
    }

    [Fact]
    public void FromUpload_RejectsUrlKey()
    {
        Should.Throw<ArgumentException>(() => Track.FromUpload(Guid.NewGuid(), Youtube, new UploadedFile("a.mp3", "b.mp3"), T0));
    }

    [Fact]
    public void AssignJob_StoresJobId()
    {
        var track = NewTrack();

        track.AssignJob(JobId, T0.AddSeconds(1));

        track.WorkerJobId.ShouldBe(JobId);
        track.UpdatedAt.ShouldBe(T0.AddSeconds(1));
    }

    [Theory]
    [InlineData("not-hex")]
    [InlineData("3F2B8C1D9E4A4F6B8A7C5D3E2F1A0B9C")]
    public void AssignJob_RejectsInvalidJobId(string jobId)
    {
        Should.Throw<ArgumentException>(() => NewTrack().AssignJob(jobId, T0));
    }

    [Fact]
    public void AssignJob_RejectsTerminalTrack()
    {
        var track = FailedTrack();

        Should.Throw<InvalidOperationException>(() => track.AssignJob(JobId, T0));
    }

    [Fact]
    public void ApplyProgress_MovesForwardAndAllowsSkips()
    {
        var track = NewTrack();

        track.ApplyProgress(new TrackProgress(TrackStatus.Separating, "Separating stems", 47), T0.AddSeconds(5));

        track.Status.ShouldBe(TrackStatus.Separating);
        track.Stage.ShouldBe("Separating stems");
        track.Progress.ShouldBe(47);
        track.UpdatedAt.ShouldBe(T0.AddSeconds(5));
    }

    [Fact]
    public void ApplyProgress_IgnoresBackwardStatus()
    {
        var track = NewTrack();
        track.ApplyProgress(new TrackProgress(TrackStatus.Analysing, "Analysing beats", 95), T0);

        track.ApplyProgress(new TrackProgress(TrackStatus.Downloading, "Downloading audio", 5), T0.AddSeconds(1));

        track.Status.ShouldBe(TrackStatus.Analysing);
        track.Progress.ShouldBe(95);
        track.Stage.ShouldBe("Analysing beats");
    }

    [Fact]
    public void ApplyProgress_NeverDecreasesProgress()
    {
        var track = NewTrack();
        track.ApplyProgress(new TrackProgress(TrackStatus.Separating, "Separating stems", 60), T0);

        track.ApplyProgress(new TrackProgress(TrackStatus.Separating, "Separating stems", 40), T0);

        track.Progress.ShouldBe(60);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(150, 100)]
    public void ApplyProgress_ClampsToRange(int reported, int expected)
    {
        var track = NewTrack();

        track.ApplyProgress(new TrackProgress(TrackStatus.Separating, null, reported), T0);

        track.Progress.ShouldBe(expected);
        track.Stage.ShouldBeNull();
    }

    [Fact]
    public void ApplyProgress_TruncatesStageTo200()
    {
        var track = NewTrack();

        track.ApplyProgress(new TrackProgress(TrackStatus.Downloading, new string('s', 300), 1), T0);

        track.Stage!.Length.ShouldBe(Track.MaxStageLength);
    }

    [Fact]
    public void ApplyProgress_IsIgnoredWhenTerminal()
    {
        var track = FailedTrack();

        track.ApplyProgress(new TrackProgress(TrackStatus.Separating, "x", 50), T0.AddHours(1));

        track.Status.ShouldBe(TrackStatus.Failed);
        track.UpdatedAt.ShouldNotBe(T0.AddHours(1));
    }

    [Theory]
    [InlineData(TrackStatus.Completed)]
    [InlineData(TrackStatus.Failed)]
    public void ApplyProgress_RejectsTerminalStatuses(TrackStatus status)
    {
        Should.Throw<ArgumentException>(() => NewTrack().ApplyProgress(new TrackProgress(status, null, 1), T0));
    }

    [Fact]
    public void ApplyMetadata_ReplacesTitleAndArtist()
    {
        var track = NewTrack();

        track.ApplyMetadata(new SourceMetadata(" Never Gonna Give You Up ", "Rick Astley"), T0.AddSeconds(2));

        track.Title.ShouldBe("Never Gonna Give You Up");
        track.Artist.ShouldBe("Rick Astley");
        track.UpdatedAt.ShouldBe(T0.AddSeconds(2));
    }

    [Fact]
    public void ApplyMetadata_OfAnUpload_KeepsTheOriginalFileNameAsTitle_AndTakesTheArtist()
    {
        var track = Track.FromUpload(Guid.NewGuid(), Upload, new UploadedFile("Song IGNORE ALL.wav", "0a1b2c.wav"), T0);

        track.ApplyMetadata(new SourceMetadata("0a1b2c", "Evil Uploader"), T0.AddSeconds(2));

        (track.Title, track.Artist).ShouldBe(("Song IGNORE ALL", "Evil Uploader"));
    }

    [Fact]
    public void ApplyMetadata_KeepsTitleWhenBlankAndTruncates()
    {
        var track = NewTrack();

        track.ApplyMetadata(new SourceMetadata("  ", new string('a', 600)), T0);

        track.Title.ShouldBe("https://youtu.be/dQw4w9WgXcQ");
        track.Artist!.Length.ShouldBe(Track.MaxArtistLength);
    }

    [Fact]
    public void ApplyMetadata_UnchangedDoesNotTouchUpdatedAt()
    {
        var track = NewTrack();
        track.ApplyMetadata(new SourceMetadata("Song", null), T0);

        track.ApplyMetadata(new SourceMetadata("Song", " "), T0.AddMinutes(1));

        track.UpdatedAt.ShouldBe(T0);
        track.Artist.ShouldBeNull();
    }

    [Fact]
    public void ApplyMetadata_IsIgnoredWhenTerminal()
    {
        var track = FailedTrack();

        track.ApplyMetadata(new SourceMetadata("Other", null), T0);

        track.Title.ShouldNotBe("Other");
    }

    [Fact]
    public void Complete_StoresResultForces100AndOrdersStems()
    {
        var track = NewTrack();
        var stems = StemCatalog.SixStem.Reverse().Select(name => new Stem(name, 10)).ToList();

        track.Complete(Completion(stems: stems), T0.AddMinutes(6));

        track.Status.ShouldBe(TrackStatus.Completed);
        track.Progress.ShouldBe(100);
        track.Stage.ShouldBe("Completed");
        track.Error.ShouldBeNull();
        track.DurationSeconds.ShouldBe(213.04);
        track.Model.ShouldBe("htdemucs_6s");
        track.Stems.Select(s => s.Name).ShouldBe(StemCatalog.SixStem);
        track.BeatGrid.ShouldNotBeNull();
        track.IsTerminal.ShouldBeTrue();
        track.UpdatedAt.ShouldBe(T0.AddMinutes(6));
    }

    [Fact]
    public void Complete_AcceptsFourStemFallback()
    {
        var track = NewTrack();

        track.Complete(Completion(model: "htdemucs", stems: StemCatalog.FourStem.Select(n => new Stem(n, 1)).ToList()), T0);

        track.Stems.Count.ShouldBe(4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(900.5)]
    public void Complete_RejectsInvalidDuration(double duration)
    {
        var error = Should.Throw<DomainValidationException>(() => NewTrack().Complete(Completion(duration: duration), T0));

        error.Errors.ShouldContain(e => e.Field == "durationSeconds");
    }

    [Fact]
    public void Complete_AcceptsExactly15Minutes()
    {
        var track = NewTrack();

        track.Complete(Completion(duration: 900), T0);

        track.DurationSeconds.ShouldBe(900);
    }

    [Fact]
    public void Complete_RejectsUnknownModel()
    {
        var error = Should.Throw<DomainValidationException>(() => NewTrack().Complete(Completion(model: "spleeter"), T0));

        error.Errors.ShouldContain(e => e.Field == "model");
    }

    [Fact]
    public void Complete_RejectsStemSetNotMatchingModel()
    {
        var fourStems = StemCatalog.FourStem.Select(n => new Stem(n, 1)).ToList();

        var error = Should.Throw<DomainValidationException>(() => NewTrack().Complete(Completion(stems: fourStems), T0));

        error.Errors.ShouldContain(e => e.Field == "stems");
        error.Message.ShouldContain("stems");
    }

    [Fact]
    public void Complete_RejectsDuplicateStems()
    {
        var stems = StemCatalog.SixStem.Select(n => new Stem(n, 1)).Append(new Stem("vocals", 1)).ToList();

        Should.Throw<DomainValidationException>(() => NewTrack().Complete(Completion(stems: stems), T0));
    }

    [Fact]
    public void Complete_RejectsNegativeStemSize()
    {
        var stems = StemCatalog.SixStem.Select(n => new Stem(n, n == "bass" ? -1 : 1)).ToList();

        var error = Should.Throw<DomainValidationException>(() => NewTrack().Complete(Completion(stems: stems), T0));

        error.Errors.ShouldContain(e => e.Field == "stems");
    }

    [Fact]
    public void Complete_RejectsInvalidBeatGrid()
    {
        var grid = ValidGrid() with { Bpm = 500 };

        var error = Should.Throw<DomainValidationException>(() => NewTrack().Complete(Completion() with { BeatGrid = grid }, T0));

        error.Errors.ShouldContain(e => e.Field == "beatGrid.bpm");
    }

    [Fact]
    public void Complete_RejectsTerminalTrack()
    {
        Should.Throw<InvalidOperationException>(() => FailedTrack().Complete(Completion(), T0));
    }

    [Fact]
    public void Fail_SetsErrorAndKeepsProgress()
    {
        var track = NewTrack();
        track.ApplyProgress(new TrackProgress(TrackStatus.Downloading, "Downloading audio", 5), T0);

        track.Fail("No matching YouTube audio was found for this Spotify track.", T0.AddSeconds(20));

        track.Status.ShouldBe(TrackStatus.Failed);
        track.Stage.ShouldBe("Failed");
        track.Progress.ShouldBe(5);
        track.Error.ShouldBe("No matching YouTube audio was found for this Spotify track.");
        track.UpdatedAt.ShouldBe(T0.AddSeconds(20));
        track.CanRetry.ShouldBeTrue();
    }

    [Fact]
    public void Fail_UsesDefaultForBlankAndTruncatesTo1000()
    {
        var blank = NewTrack();
        var longError = NewTrack();

        blank.Fail(" ", T0);
        longError.Fail(new string('e', 2000), T0);

        blank.Error.ShouldBe("Processing failed.");
        longError.Error!.Length.ShouldBe(Track.MaxErrorLength);
    }

    [Fact]
    public void Fail_RejectsCompletedTrack()
    {
        var track = NewTrack();
        track.Complete(Completion(), T0);

        Should.Throw<InvalidOperationException>(() => track.Fail("x", T0));
        track.CanRetry.ShouldBeFalse();
    }

    [Fact]
    public void Retry_FromFailedResetsToQueued()
    {
        var track = FailedTrack();

        track.Retry(T0.AddHours(1));

        track.Status.ShouldBe(TrackStatus.Queued);
        track.Stage.ShouldBe("Queued");
        track.Progress.ShouldBe(0);
        track.Error.ShouldBeNull();
        track.WorkerJobId.ShouldBeNull();
        track.UpdatedAt.ShouldBe(T0.AddHours(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retry_IsOnlyAllowedFromFailed(bool completed)
    {
        var track = NewTrack();
        if (completed)
        {
            track.Complete(Completion(), T0);
        }

        Should.Throw<InvalidOperationException>(() => track.Retry(T0));
    }

    [Fact]
    public void StemCatalog_KnowsNamesAndModels()
    {
        StemCatalog.IsKnown("guitar").ShouldBeTrue();
        StemCatalog.IsKnown("Guitar").ShouldBeFalse();
        StemCatalog.ForModel("fake").ShouldBe(StemCatalog.SixStem);
        StemCatalog.ForModel("htdemucs").ShouldBe(StemCatalog.FourStem);
        StemCatalog.ForModel("unknown").ShouldBeNull();
    }

    internal static TrackCompletion Completion(double duration = 213.04, string model = "htdemucs_6s", IReadOnlyList<Stem>? stems = null) =>
        new(duration, model, stems ?? StemCatalog.SixStem.Select(n => new Stem(n, 1000)).ToList(), ValidGrid());

    internal static BeatGrid ValidGrid() => new()
    {
        Bpm = 113.5,
        Beats = [0.52, 1.05, 1.58, 2.11, 2.64],
        Downbeats = [0.52, 2.64],
        BeatsPerBar = 4,
        Confidence = 0.87,
        Reliable = true,
    };

    private static Track NewTrack() => Track.FromUrl(Guid.NewGuid(), Youtube, " https://youtu.be/dQw4w9WgXcQ ", T0);

    private static Track FailedTrack()
    {
        var track = NewTrack();
        track.Fail("boom", T0);
        return track;
    }
}
