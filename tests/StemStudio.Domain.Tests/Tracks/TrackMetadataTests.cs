using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Tracks;

/// <summary>Feature 005 T016: the lookup state machine (data-model 3.4, FR-009, FR-012).</summary>
public sealed class TrackMetadataTests
{
    private const string MetadataJobId = "5d2c7e9f1a3b4c6d8e0f2a4b6c8d0e1f";
    private static readonly DateTime T0 = DetailTestData.T0;
    private static readonly Guid RecordingMbid = Guid.Parse("0f4e8a7c-3b2d-4c1e-9f8a-7b6c5d4e3f2a");
    private static readonly Guid ReleaseGroupMbid = Guid.Parse("1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d");
    private static readonly Guid ArtistMbid = Guid.Parse("6b7c8d9e-0f1a-4b2c-8d3e-4f5a6b7c8d9e");

    [Fact]
    public void NewTrack_HasStatusNone()
    {
        var track = DetailTestData.CompletedTrack();

        track.Metadata.ShouldBe(TrackMetadata.None);
        track.Metadata.Status.ShouldBe(MetadataStatus.None);
        track.IsLookupActive.ShouldBeFalse();
        track.ArtistId.ShouldBeNull();
    }

    [Fact]
    public void ScheduleLookup_FromNoneStoresTheQuery()
    {
        var track = DetailTestData.CompletedTrack();

        track.ScheduleLookup(new LookupQuery(" Hero of the Day ", "Metallica", "Load", 262.3, "USEE19600011"), T0.AddMinutes(1));

        track.Metadata.Status.ShouldBe(MetadataStatus.Pending);
        track.Metadata.LookupTitle.ShouldBe("Hero of the Day");
        track.Metadata.LookupArtist.ShouldBe("Metallica");
        track.Metadata.LookupAlbum.ShouldBe("Load");
        track.Metadata.LookupDurationSeconds.ShouldBe(262.3);
        track.Metadata.Isrc.ShouldBe("USEE19600011");
        track.Metadata.UpdatedAt.ShouldBe(T0.AddMinutes(1));
        track.UpdatedAt.ShouldBe(T0.AddMinutes(1));
        track.IsLookupActive.ShouldBeTrue();
    }

    [Fact]
    public void ScheduleLookup_BlankTitleFallsBackToTheTrackTitle()
    {
        var track = DetailTestData.CompletedTrack();

        track.ScheduleLookup(new LookupQuery(" ", null), T0);

        track.Metadata.LookupTitle.ShouldBe(track.Title);
    }

    [Fact]
    public void ScheduleLookup_IsIgnoredOnceALookupHappened()
    {
        var track = Searching();

        track.ScheduleLookup(new LookupQuery("Other", null), T0.AddHours(1));

        track.Metadata.Status.ShouldBe(MetadataStatus.Searching);
        track.Metadata.LookupTitle.ShouldBe("Hero of the Day");
    }

    [Fact]
    public void ScheduleOwnLookup_UsesTitleArtistAndDurationOfTheTrack()
    {
        var track = DetailTestData.CompletedTrack();

        track.ScheduleOwnLookup(T0);

        track.Metadata.Status.ShouldBe(MetadataStatus.Pending);
        track.Metadata.LookupTitle.ShouldBe(track.Title);
        track.Metadata.LookupDurationSeconds.ShouldBe(track.DurationSeconds);
        track.Metadata.Isrc.ShouldBeNull();
    }

    [Fact]
    public void StartLookup_MovesPendingToSearching()
    {
        var track = Searching();

        track.Metadata.Status.ShouldBe(MetadataStatus.Searching);
        track.Metadata.MetadataJobId.ShouldBe(MetadataJobId);
    }

    [Fact]
    public void StartLookup_RequiresPendingAndAJobId()
    {
        Should.Throw<InvalidOperationException>(() => DetailTestData.CompletedTrack().StartLookup(MetadataJobId, T0));
        Should.Throw<ArgumentException>(() => Pending().StartLookup("nope", T0));
    }

    [Fact]
    public void CompleteLookup_FoundStoresTheFactsAndTheArtist()
    {
        var track = Searching();

        track.CompleteLookup(Found(), T0.AddMinutes(2));

        var metadata = track.Metadata;
        metadata.Status.ShouldBe(MetadataStatus.Found);
        metadata.RecordingMbid.ShouldBe(RecordingMbid);
        metadata.RecordingTitle.ShouldBe("Hero of the Day");
        metadata.ReleaseGroupMbid.ShouldBe(ReleaseGroupMbid);
        metadata.Album.ShouldBe("Load");
        metadata.Year.ShouldBe(1996);
        metadata.ArtistName.ShouldBe("Metallica");
        metadata.Reason.ShouldBeNull();
        metadata.MetadataJobId.ShouldBe(MetadataJobId);
        track.ArtistId.ShouldBe(ArtistMbid);
        track.IsLookupActive.ShouldBeFalse();
    }

    [Fact]
    public void CompleteLookup_KeepsWarningsAsTheReasonAndDropsAnImpossibleYear()
    {
        var track = Searching();

        track.CompleteLookup(Found() with { Year = 99, Warnings = "Wikipedia could not be reached." }, T0);

        track.Metadata.Year.ShouldBeNull();
        track.Metadata.Reason.ShouldBe("Wikipedia could not be reached.");
    }

    [Theory]
    [InlineData(LookupOutcome.NotFound, MetadataStatus.NotFound)]
    [InlineData(LookupOutcome.Disabled, MetadataStatus.Disabled)]
    public void CompleteLookup_WithoutMatchClearsTheFacts(LookupOutcome outcome, MetadataStatus status)
    {
        var track = Found(Searching());
        track.RetryLookup(null, null, T0.AddMinutes(5));
        track.StartLookup(MetadataJobId, T0.AddMinutes(5));

        track.CompleteLookup(new LookupResult(outcome), T0.AddMinutes(6));

        track.Metadata.Status.ShouldBe(status);
        track.Metadata.Album.ShouldBeNull();
        track.Metadata.ArtistName.ShouldBeNull();
        track.ArtistId.ShouldBeNull();
    }

    [Fact]
    public void CompleteLookup_RequiresSearching()
    {
        Should.Throw<InvalidOperationException>(() => Pending().CompleteLookup(Found(), T0));
    }

    [Fact]
    public void FailLookup_FromSearchingOrPendingKeepsTheReason()
    {
        var searching = Searching();
        var pending = Pending();

        searching.FailLookup("Band information could not be retrieved; try again later.", T0.AddMinutes(1));
        pending.FailLookup(" ", T0.AddMinutes(1));

        searching.Metadata.Status.ShouldBe(MetadataStatus.Failed);
        searching.Metadata.Reason.ShouldBe("Band information could not be retrieved; try again later.");
        pending.Metadata.Reason.ShouldBe(Track.LookupFailedDefault);
    }

    [Fact]
    public void FailLookup_CutsTheReasonTo1000Characters()
    {
        var track = Searching();

        track.FailLookup(new string('x', 1500), T0);

        track.Metadata.Reason!.Length.ShouldBe(1000);
    }

    [Fact]
    public void FailLookup_RequiresAnActiveLookup()
    {
        Should.Throw<InvalidOperationException>(() => DetailTestData.CompletedTrack().FailLookup("x", T0));
    }

    [Theory]
    [InlineData(MetadataStatus.Failed)]
    [InlineData(MetadataStatus.NotFound)]
    [InlineData(MetadataStatus.Found)]
    [InlineData(MetadataStatus.Disabled)]
    public void RetryLookup_FromAFinishedLookupGoesBackToPending(MetadataStatus from)
    {
        var track = Finished(from);

        track.RetryLookup(null, null, T0.AddHours(1));

        track.Metadata.Status.ShouldBe(MetadataStatus.Pending);
        track.Metadata.MetadataJobId.ShouldBeNull();
        track.Metadata.Reason.ShouldBeNull();
        track.Metadata.LookupTitle.ShouldBe("Hero of the Day");
        track.Metadata.Isrc.ShouldBe("USEE19600011");
        track.Metadata.CorrectedByUser.ShouldBeFalse();
    }

    [Fact]
    public void RetryLookup_WithCorrectedNamesStoresThemAndDropsTheIsrc()
    {
        var track = Finished(MetadataStatus.NotFound);

        track.RetryLookup(" Hero Of The Day ", " Metallica ", T0.AddHours(1));

        track.Metadata.LookupTitle.ShouldBe("Hero Of The Day");
        track.Metadata.LookupArtist.ShouldBe("Metallica");
        track.Metadata.CorrectedByUser.ShouldBeTrue();
        track.Metadata.Isrc.ShouldBeNull();
    }

    [Fact]
    public void RetryLookup_WithOnlyATitleKeepsTheArtist()
    {
        var track = Finished(MetadataStatus.NotFound);

        track.RetryLookup("New title", null, T0.AddHours(1));

        track.Metadata.LookupTitle.ShouldBe("New title");
        track.Metadata.LookupArtist.ShouldBe("Metallica");
    }

    [Fact]
    public void RetryLookup_OfANeverLookedUpTrackUsesItsOwnNames()
    {
        var track = DetailTestData.CompletedTrack();

        track.RetryLookup(null, null, T0);

        track.Metadata.Status.ShouldBe(MetadataStatus.Pending);
        track.Metadata.LookupTitle.ShouldBe(track.Title);
    }

    [Fact]
    public void RetryLookup_WhilePendingUpdatesTheNames()
    {
        var track = Pending();

        track.RetryLookup(null, "Other Artist", T0.AddMinutes(1));

        track.Metadata.Status.ShouldBe(MetadataStatus.Pending);
        track.Metadata.LookupArtist.ShouldBe("Other Artist");
    }

    [Fact]
    public void RetryLookup_IsRejectedWhileSearching()
    {
        var track = Searching();

        track.CanRetryLookup.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => track.RetryLookup(null, null, T0));
    }

    private static Track Pending()
    {
        var track = DetailTestData.CompletedTrack();
        track.ScheduleLookup(new LookupQuery("Hero of the Day", "Metallica", "Load", 262.3, "USEE19600011"), T0);
        return track;
    }

    private static Track Searching()
    {
        var track = Pending();
        track.StartLookup(MetadataJobId, T0.AddSeconds(1));
        return track;
    }

    private static Track Found(Track track)
    {
        track.CompleteLookup(Found(), T0.AddMinutes(1));
        return track;
    }

    private static Track Finished(MetadataStatus status)
    {
        var track = Searching();
        switch (status)
        {
            case MetadataStatus.Failed:
                track.FailLookup("down", T0.AddMinutes(1));
                break;
            case MetadataStatus.Found:
                track.CompleteLookup(Found(), T0.AddMinutes(1));
                break;
            default:
                track.CompleteLookup(new LookupResult(status == MetadataStatus.NotFound ? LookupOutcome.NotFound : LookupOutcome.Disabled), T0.AddMinutes(1));
                break;
        }

        return track;
    }

    private static LookupResult Found() => new(LookupOutcome.Found)
    {
        RecordingMbid = RecordingMbid,
        RecordingTitle = "Hero of the Day",
        ReleaseGroupMbid = ReleaseGroupMbid,
        Album = "Load",
        Year = 1996,
        ArtistMbid = ArtistMbid,
        ArtistName = "Metallica",
    };
}
