using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Feature 006 T021: lyrics in SQLite (data-model 3.1, migration AddTrackLyrics; self-written text).</summary>
public sealed class LyricsPersistenceTests
{
    private const string LastMigrationOf007 = "20261115152313_AddAssets";
    private const string LyricsJobId = "9a4c6e8b0d2f41a3b5c7d9e1f3a5b7c9";
    private static readonly DateTime T0 = TestData.T0;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FoundLyrics_RoundTripTheDocumentOffsetAndDevice()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = WithLyrics(LyricsStatus.Found);
        track.Lyrics!.SetOffset(-120, T0.AddMinutes(1));

        var loaded = await db.SaveAndReloadAsync(track);

        var lyrics = loaded.Lyrics!;
        (loaded.LyricsStatus, lyrics.TrackId, lyrics.Status, lyrics.OffsetMs, lyrics.UpdatedAt).ShouldBe((LyricsStatus.Found, track.Id, LyricsStatus.Found, -120, T0.AddMinutes(1)));
        Json(lyrics.Document!).ShouldBe(Json(WordDocument()));
        (await db.ScalarAsync("SELECT Status FROM TrackLyrics")).ShouldBe("Found");
    }

    [Fact]
    public async Task AligningLyrics_RoundTripJobProgressDeviceAndEstimate()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = WithLyrics(LyricsStatus.Aligning);
        var gpu = new JobDevice(ProcessingDeviceKind.Gpu, "NVIDIA GeForce RTX 5070 Ti", false, null);
        track.Lyrics!.AssignJob(LyricsJobId, T0);
        track.Lyrics.ApplyAlignment(new AlignmentReport(40, gpu, 30), T0);

        var lyrics = (await db.SaveAndReloadAsync(track)).Lyrics!;

        (lyrics.Status, lyrics.LyricsJobId, lyrics.AlignProgress, lyrics.AlignDevice, lyrics.AlignEstimateSeconds).ShouldBe((LyricsStatus.Aligning, LyricsJobId, 40, gpu, 30));
        lyrics.Document!.SyncLevel.ShouldBe(SyncLevel.Plain);
    }

    [Fact]
    public async Task TheStatusColumn_ServesListsWithoutLoadingTheText()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await db.SeedAsync(WithLyrics(LyricsStatus.Found), TestData.CompletedTrack("aaaaaaaaaaa"));

        await using var read = db.NewContext();
        var listed = await new TrackRepository(read).ListAsync(Ct);

        listed.Select(track => track.LyricsStatus).ShouldBe([LyricsStatus.Found, LyricsStatus.None], ignoreOrder: true);
        listed.ShouldAllBe(track => track.Lyrics == null);
        (await db.ScalarAsync("SELECT group_concat(LyricsStatus) FROM (SELECT LyricsStatus FROM Tracks ORDER BY LyricsStatus)")).ShouldBe("Found,None");
    }

    [Fact]
    public async Task ChangesThroughTheLoadedRow_UpdateTheMirroredStatus()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = WithLyrics(LyricsStatus.Found);
        await db.SeedAsync(track);

        await using (var write = db.NewContext())
        {
            var tracks = new TrackRepository(write);
            var tracked = (await tracks.FindAsync(track.Id, Ct))!;
            tracked.Lyrics!.Remove(T0.AddMinutes(2));
            await tracks.SaveAsync(Ct);
        }

        (await db.ScalarAsync("SELECT LyricsStatus FROM Tracks")).ShouldBe("Removed");
        (await db.ScalarAsync("SELECT Document IS NULL FROM TrackLyrics")).ShouldBe("1");
    }

    [Fact]
    public async Task ANewRowOnALoadedTrack_IsInserted()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);

        await using (var write = db.NewContext())
        {
            var tracks = new TrackRepository(write);
            var tracked = (await tracks.FindAsync(track.Id, Ct))!;
            tracked.Lyrics.ShouldBeNull();
            tracked.ScheduleLyricsLookup(T0.AddMinutes(1));
            await tracks.SaveAsync(Ct);
        }

        (await db.ScalarAsync("SELECT Status || ':' || (SELECT LyricsStatus FROM Tracks) FROM TrackLyrics")).ShouldBe("Searching:Searching");
    }

    [Fact]
    public async Task ListLyricsActive_ReturnsSearchesAlignmentsAndPendingAutomaticRulesWithTheirRows()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var declined = WithLyrics(LyricsStatus.AlignPending, "ddddddddddd");
        declined.Lyrics!.DeclineAutoAlign(420, "Aligning takes about 7 minutes on the CPU.", T0);
        var (searching, aligning, pending) = (WithLyrics(LyricsStatus.Searching, "aaaaaaaaaaa"), WithLyrics(LyricsStatus.Aligning, "bbbbbbbbbbb"), WithLyrics(LyricsStatus.AlignPending, "ccccccccccc"));
        await db.SeedAsync(searching, aligning, pending, declined, WithLyrics(LyricsStatus.Found, "eeeeeeeeeee"), TestData.CompletedTrack("fffffffffff"));

        await using var read = db.NewContext();
        var active = await new TrackRepository(read).ListLyricsActiveAsync(Ct);

        active.Select(track => track.Id).ShouldBe([searching.Id, aligning.Id, pending.Id], ignoreOrder: true);
        active.ShouldAllBe(track => track.Lyrics != null);
    }

    [Fact]
    public async Task RemovingATrack_DeletesItsLyrics()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (removed, kept) = (WithLyrics(LyricsStatus.Found), WithLyrics(LyricsStatus.Found, "aaaaaaaaaaa"));
        await db.SeedAsync(removed, kept);

        await using (var write = db.NewContext())
        {
            var tracks = new TrackRepository(write);
            await tracks.RemoveWithMixesAsync((await tracks.FindAsync(removed.Id, Ct))!, Ct);
        }

        (await db.ScalarAsync("SELECT COUNT(*) FROM TrackLyrics")).ShouldBe("1");
        await db.ScalarAsync("DELETE FROM Tracks");
        (await db.ScalarAsync("SELECT COUNT(*) FROM TrackLyrics")).ShouldBe("0");
    }

    [Fact]
    public async Task DatabaseFrom007_MigratesWithStatusNoneAndNoRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-007-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            await MigrateToAsync(connectionString, LastMigrationOf007);

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            (await db.ScalarAsync("SELECT LyricsStatus FROM Tracks")).ShouldBe("None");
            (await db.ScalarAsync("SELECT COUNT(*) FROM TrackLyrics")).ShouldBe("0");
            await using var read = db.NewContext();
            read.Database.GetPendingMigrations().ShouldBeEmpty();
            var track = (await new TrackRepository(read).ListAsync(Ct)).Single();
            (track.LyricsStatus, track.Lyrics).ShouldBe((LyricsStatus.None, null));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static async Task MigrateToAsync(string connectionString, string migration)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var context = new StemStudioDbContext(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(connection).Options);
        await context.GetService<IMigrator>().MigrateAsync(migration, Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Tracks (Id, SourceType, SourceKey, Title, Status, Progress, CreatedAt, UpdatedAt, Stems, DetailPhase, Metadata)
            VALUES ('0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a', 'Youtube', 'youtube:dQw4w9WgXcQ', 'Song', 'Completed', 100, '2026-11-14 12:00:00', '2026-11-14 12:00:00', '[]', NULL, '{"Status":"None","CorrectedByUser":false}')
            """;
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static Track WithLyrics(LyricsStatus status, string videoId = "dQw4w9WgXcQ")
    {
        var track = TestData.CompletedTrack(videoId);
        var lyrics = track.EnsureLyrics(T0);
        lyrics.Search(T0);
        if (status == LyricsStatus.Searching)
        {
            return track;
        }

        lyrics.ApplyLookup(LyricsOutcome.Found, status == LyricsStatus.Found ? WordDocument() : PlainDocument(), T0);
        if (status == LyricsStatus.Aligning)
        {
            lyrics.StartAlign(T0);
        }

        return track;
    }

    private static LyricsDocument WordDocument() => new()
    {
        SyncLevel = SyncLevel.Word,
        Origin = LyricsOrigin.Source,
        Language = "en",
        Source = LyricsSource.Lrclib,
        SourceId = "1000001",
        SourceDurationSeconds = 213.0,
        Lines = [new LyricsLine(10950, 13200, "Fake line one", [new LyricsWord("Fake", 10950, 11400, WordOrigin.Source, false), new LyricsWord("line", 11400, 11900, WordOrigin.Source, true), new LyricsWord("one", 11900, 13200, WordOrigin.Source, false)])],
        PlainText = "Fake line one",
    };

    private static LyricsDocument PlainDocument() => LyricsDocument.FromUserText("Fake line one\nLa la la", "en") with { Source = LyricsSource.Lrclib, SourceId = "1000004" };

    private static string Json(LyricsDocument document) => JsonSerializer.Serialize(document);
}
