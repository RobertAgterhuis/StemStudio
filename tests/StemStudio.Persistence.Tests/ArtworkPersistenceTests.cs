using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StemStudio.Domain.Artists;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Feature 005 T018: artwork, lookup state and artists in SQLite (data-model 3.1-3.3, migration AddArtworkAndArtists).</summary>
public sealed class ArtworkPersistenceTests
{
    private const string LastMigrationOf003 = "20261115115115_AddJobDevice";
    private const string MetadataJobId = "5d2c7e9f1a3b4c6d8e0f2a4b6c8d0e1f";
    private static readonly string HashA = new('a', 64);
    private static readonly string HashB = new('b', 64);
    private static readonly Guid ArtistMbid = Guid.Parse("6b7c8d9e-0f1a-4b2c-8d3e-4f5a6b7c8d9e");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Artwork_RoundTripsUserAndAutomaticPicture()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        track.OfferArtwork(new ArtworkCandidate(ArtworkSource.Spotify, HashA), TestData.T0);
        track.SetUserArtwork(HashB, TestData.T0.AddMinutes(1));

        var loaded = await db.SaveAndReloadAsync(track);

        loaded.Artwork.ShouldBe(new TrackArtwork(ArtworkSource.User, HashB, TestData.T0.AddMinutes(1)));
        loaded.AutoArtwork.ShouldBe(new TrackArtwork(ArtworkSource.Spotify, HashA, TestData.T0));
        (await db.ScalarAsync("SELECT AutoArtwork FROM Tracks"))!.ShouldContain("\"Spotify\"");
    }

    [Fact]
    public async Task TrackWithoutArtwork_StoresNull()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        var loaded = await db.SaveAndReloadAsync(TestData.CompletedTrack());

        loaded.Artwork.ShouldBeNull();
        loaded.AutoArtwork.ShouldBeNull();
        (await db.ScalarAsync("SELECT Artwork IS NULL AND AutoArtwork IS NULL FROM Tracks")).ShouldBe("1");
    }

    [Fact]
    public async Task Metadata_RoundTripsEveryField()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await SeedArtistAsync(db);
        var track = FoundTrack();

        var loaded = await db.SaveAndReloadAsync(track);

        loaded.Metadata.ShouldBe(track.Metadata);
        loaded.ArtistId.ShouldBe(ArtistMbid);
        (await db.ScalarAsync("SELECT Metadata FROM Tracks"))!.ShouldContain("\"Found\"");
    }

    [Fact]
    public async Task ChangingTheMetadataOfATrackedTrack_IsSaved()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);

        await using (var context = db.NewContext())
        {
            var repository = new TrackRepository(context);
            var tracked = (await repository.FindAsync(track.Id, Ct))!;
            tracked.ScheduleOwnLookup(TestData.T0.AddHours(1));
            tracked.StartLookup(MetadataJobId, TestData.T0.AddHours(1));
            await repository.SaveAsync(Ct);
        }

        await using var read = db.NewContext();
        (await new TrackRepository(read).FindAsync(track.Id, Ct))!.Metadata.Status.ShouldBe(MetadataStatus.Searching);
    }

    [Fact]
    public async Task ListLookupActive_ReturnsPendingAndSearchingTracksOldestFirst()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (pending, searching, found, none) = (LookupTrack("aaaaaaaaaaa", 2), LookupTrack("bbbbbbbbbbb", 1), FoundTrack("ccccccccccc"), TestData.CompletedTrack("ddddddddddd"));
        searching.StartLookup(MetadataJobId, TestData.T0);
        await SeedArtistAsync(db);
        await db.SeedAsync(pending, searching, found, none);

        await using var context = db.NewContext();
        var active = await new TrackRepository(context).ListLookupActiveAsync(Ct);

        active.Select(track => track.Id).ShouldBe([searching.Id, pending.Id]);
    }

    [Fact]
    public async Task Artist_RoundTripsEveryFact()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await SeedArtistAsync(db);

        await using var context = db.NewContext();
        var artist = (await new ArtistRepository(context).FindAsync(ArtistMbid, Ct))!;

        artist.Name.ShouldBe("The Example Band");
        artist.Type.ShouldBe(ArtistType.Group);
        artist.Genres.ShouldBe(["heavy metal", "thrash metal"]);
        artist.Members.ShouldBe([new ArtistMember("First Member", 1981), new ArtistMember("Second Member", 1983, 2001)]);
        artist.Summary.ShouldBe(Facts().Summary);
        artist.FetchedAt.ShouldBe(TestData.T0);
        (await new ArtistRepository(context).FindAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Artist_WithoutOptionalFactsRoundTrips()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using (var write = db.NewContext())
        {
            var artists = new ArtistRepository(write);
            artists.Add(Artist.Create(new ArtistFacts { Mbid = ArtistMbid, Name = "Solo", FetchedAt = TestData.T0 }));
            await artists.SaveAsync(Ct);
        }

        await using var read = db.NewContext();
        var artist = (await new ArtistRepository(read).FindAsync(ArtistMbid, Ct))!;
        (artist.Type, artist.Summary, artist.Genres.Count, artist.Members.Count).ShouldBe((null, null, 0, 0));
    }

    [Fact]
    public async Task RefreshingATrackedArtist_IsSaved()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await SeedArtistAsync(db);
        await using (var write = db.NewContext())
        {
            var artists = new ArtistRepository(write);
            (await artists.FindAsync(ArtistMbid, Ct))!.Refresh(Facts() with { Genres = ["rock"], Members = [], FetchedAt = TestData.T0.AddDays(40) });
            await artists.SaveAsync(Ct);
        }

        await using var read = db.NewContext();
        var artist = (await new ArtistRepository(read).FindAsync(ArtistMbid, Ct))!;
        (artist.Genres.Single(), artist.Members.Count).ShouldBe(("rock", 0));
    }

    [Fact]
    public async Task DeletingAnArtistRow_SetsTheTrackReferenceToNull()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await SeedArtistAsync(db);
        var track = FoundTrack();
        await db.SeedAsync(track);

        await db.ScalarAsync("DELETE FROM Artists");

        (await db.ScalarAsync("SELECT ArtistId IS NULL FROM Tracks")).ShouldBe("1");
    }

    [Fact]
    public async Task RemovingTheLastTrackOfAnArtist_DeletesTheArtistInTheSameSave()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await SeedArtistAsync(db);
        var (first, second) = (FoundTrack("aaaaaaaaaaa"), FoundTrack("bbbbbbbbbbb"));
        await db.SeedAsync(first, second);

        await RemoveAsync(db, first.Id);
        (await db.ScalarAsync("SELECT COUNT(*) FROM Artists")).ShouldBe("1");

        await RemoveAsync(db, second.Id);
        (await db.ScalarAsync("SELECT COUNT(*) FROM Artists")).ShouldBe("0");
    }

    [Fact]
    public async Task RemoveIfUnused_DeletesOnlyUnreferencedArtists()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await SeedArtistAsync(db);
        await db.SeedAsync(FoundTrack());

        await using (var context = db.NewContext())
        {
            var artists = new ArtistRepository(context);
            (await artists.RemoveIfUnusedAsync(ArtistMbid, Ct)).ShouldBeFalse();
            (await artists.RemoveIfUnusedAsync(Guid.NewGuid(), Ct)).ShouldBeFalse();
        }

        await db.ScalarAsync("UPDATE Tracks SET ArtistId = NULL");
        await using var again = db.NewContext();
        (await new ArtistRepository(again).RemoveIfUnusedAsync(ArtistMbid, Ct)).ShouldBeTrue();
        (await db.ScalarAsync("SELECT COUNT(*) FROM Artists")).ShouldBe("0");
    }

    [Fact]
    public async Task DatabaseFrom003_MigratesAndLoadsWithoutArtworkAndStatusNone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-003-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            var trackId = await Create003DatabaseAsync(connectionString);

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = db.NewContext();
            var track = (await new TrackRepository(read).FindAsync(trackId, Ct))!;

            (track.Metadata, track.Artwork, track.AutoArtwork, track.ArtistId).ShouldBe((TrackMetadata.None, null, null, null));
            track.Device!.Kind.ShouldBe(ProcessingDeviceKind.Cpu);
            read.Database.GetPendingMigrations().ShouldBeEmpty();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    internal static ArtistFacts Facts() => new()
    {
        Mbid = ArtistMbid,
        Name = "The Example Band",
        Type = ArtistType.Group,
        Country = "US",
        BeginYear = 1981,
        Genres = ["heavy metal", "thrash metal"],
        Members = [new ArtistMember("First Member", 1981), new ArtistMember("Second Member", 1983, 2001)],
        Website = "https://www.example.org/",
        WikidataId = "Q15920",
        Summary = new WikipediaSummary("en", "The Example Band", "A placeholder.", "https://en.wikipedia.org/wiki/X", "https://en.wikipedia.org/w/index.php?title=X&action=history", "1"),
        FetchedAt = TestData.T0,
    };

    private static async Task SeedArtistAsync(SqliteTestDatabase db)
    {
        await using var context = db.NewContext();
        var artists = new ArtistRepository(context);
        artists.Add(Artist.Create(Facts()));
        await artists.SaveAsync(Ct);
    }

    private static async Task RemoveAsync(SqliteTestDatabase db, Guid trackId)
    {
        await using var context = db.NewContext();
        var tracks = new TrackRepository(context);
        await tracks.RemoveWithMixesAsync((await tracks.FindAsync(trackId, Ct))!, Ct);
    }

    private static Track LookupTrack(string videoId, int minutes)
    {
        var track = TestData.QueuedTrack(videoId, TestData.T0.AddMinutes(minutes));
        track.ScheduleLookup(new LookupQuery("Song", "Artist", null, 200), TestData.T0);
        return track;
    }

    private static Track FoundTrack(string videoId = "dQw4w9WgXcQ")
    {
        var track = TestData.CompletedTrack(videoId);
        track.ScheduleLookup(new LookupQuery("Song", "Artist", "Album", 200, "USEE19600011"), TestData.T0);
        track.StartLookup(MetadataJobId, TestData.T0);
        track.CompleteLookup(
            new LookupResult(LookupOutcome.Found)
            {
                RecordingMbid = Guid.NewGuid(),
                RecordingTitle = "Song",
                ReleaseGroupMbid = Guid.NewGuid(),
                Album = "Album",
                Year = 1996,
                ArtistMbid = ArtistMbid,
                ArtistName = "The Example Band",
                Warnings = "Wikipedia could not be reached.",
            },
            TestData.T0.AddSeconds(9));
        return track;
    }

    private static async Task<Guid> Create003DatabaseAsync(string connectionString)
    {
        var trackId = Guid.NewGuid();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using (var context = new StemStudioDbContext(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(connection).Options))
        {
            await context.GetService<IMigrator>().MigrateAsync(LastMigrationOf003, Ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = Rows003Sql;
        command.Parameters.AddWithValue("$track", trackId.ToString("D").ToUpperInvariant());
        await command.ExecuteNonQueryAsync(Ct);
        return trackId;
    }

    private const string Rows003Sql = """
        INSERT INTO Tracks (Id, SourceType, SourceKey, SourceUrl, Title, Artist, Status, Stage, Progress, Error, WorkerJobId,
            UploadFileName, DurationSeconds, Model, CreatedAt, UpdatedAt, BeatGrid, Stems, DetailPhase, Device)
        VALUES ($track, 'Youtube', 'youtube:dQw4w9WgXcQ', 'https://youtu.be/dQw4w9WgXcQ', 'Song', NULL, 'Completed', 'Completed', 100,
            NULL, '3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c', NULL, 213.04, 'htdemucs_6s', '2026-11-14 12:00:00', '2026-11-14 12:06:00',
            '{"Beats":[0.52,1.05],"BeatsPerBar":4,"Bpm":113.5,"Confidence":0.87,"Downbeats":[0.52],"Reliable":true}',
            '[{"Name":"vocals","SizeBytes":1},{"Name":"drums","SizeBytes":1},{"Name":"bass","SizeBytes":1},{"Name":"guitar","SizeBytes":1},{"Name":"piano","SizeBytes":1},{"Name":"other","SizeBytes":1}]',
            NULL, '{"Kind":"Cpu","Name":null,"CpuFallback":false,"FallbackReason":null}');
        """;
}
