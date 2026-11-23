using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Feature 008 T034: score items in SQLite (data-model 4.1, migration AddScoreFacts).</summary>
public sealed class ScorePersistenceTests
{
    private const string LastMigrationOf006 = "20261115163349_AddTrackLyrics";
    private const string JobId = "c4e6a8b0d2f4416385a7c9e1b3d5f7a9";
    private static readonly DateTime T0 = TestData.T0;
    private static readonly Guid TrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AReadyScore_RoundTripsItsFactsFilesAndRecipe()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var score = ReadyScore();

        await AddAsync(db, score);

        await using var read = db.NewContext();
        var loaded = (await new AssetRepository(read).FindAsync(score.Id, Ct))!;
        (loaded.Kind, loaded.Status, loaded.Audio, loaded.Provenance.Recipe, loaded.Provenance.Origin).ShouldBe((AssetKind.Score, AssetStatus.Ready, null, null, AssetOrigin.Transcription));
        Json(loaded.Score!).ShouldBe(Json(score.Score!));
        Json(loaded.Provenance.ScoreRecipe!).ShouldBe(Json(score.Provenance.ScoreRecipe!));
        loaded.Files.Select(file => file.Role).ShouldBe(["pdf", "musicxml", "midi"]);
        (await db.ScalarAsync("SELECT Score IS NOT NULL FROM Assets")).ShouldBe("1");
    }

    [Fact]
    public async Task TheKindFilter_SeparatesScoresFromAudio()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (score, audio) = (ReadyScore(), Asset.Create(Guid.NewGuid(), "Song - drums", AudioProvenance(), T0));
        await AddAsync(db, score, audio);

        await using var read = db.NewContext();
        var assets = new AssetRepository(read);
        (await assets.ListAsync(new AssetQuery { Kind = AssetKind.Score }, Ct)).Items.Single().Id.ShouldBe(score.Id);
        (await assets.ListAsync(new AssetQuery { Kind = AssetKind.Audio }, Ct)).Items.Single().Score.ShouldBeNull();
        (await db.ScalarAsync("SELECT COUNT(*) FROM Assets WHERE Score IS NULL")).ShouldBe("1");
    }

    [Fact]
    public async Task DatabaseFrom006_LoadsItsAudioItemsWithoutScore()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-006-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            await SqliteTestDatabase.MigrateLegacyAsync(connectionString, LastMigrationOf006);
            await InsertLegacyAssetAsync(connectionString);

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = db.NewContext();
            read.Database.GetPendingMigrations().ShouldBeEmpty();
            var asset = (await new AssetRepository(read).ListAsync(new AssetQuery(), Ct)).Items.Single();
            (asset.Kind, asset.Score, asset.Provenance.ScoreRecipe, asset.Provenance.Recipe!.Format.Format).ShouldBe((AssetKind.Audio, null, null, AudioFormat.Flac));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static async Task InsertLegacyAssetAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Assets (Id, Kind, Name, Status, Progress, Error, WorkerJobId, Audio, Provenance, TrackId, SizeBytes, CreatedAt, UpdatedAt, Files)
            VALUES ('3c9a1e7b-5d2f-4a6c-8e0b-1f2a3b4c5d6e', 'Audio', 'Song - drums', 'Queued', 0, NULL, NULL, NULL,
              '{"Origin":"Export","TrackId":"0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a","TrackTitle":"Song","TrackArtist":null,"SourceAssetIds":[],"Contents":"drums, raw","Recipe":{"TrackId":"0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a","Mode":"Raw","Channels":["drums"],"Mix":null,"Range":null,"Format":{"Format":"Flac","BitDepth":16,"Mp3BitrateKbps":null}}}',
              '0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a', 0, '2026-11-15 12:00:00', '2026-11-15 12:00:00', '[]')
            """;
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static Asset ReadyScore()
    {
        var asset = Asset.CreateScore(Guid.NewGuid(), "Song - bass (sheet music)", ScoreProvenance(), T0);
        asset.AssignJob(JobId, T0);
        asset.CompleteScore(
            [
                new AssetFile("pdf", "score.pdf", "application/pdf", 100, new string('a', 64)),
                new AssetFile("musicxml", "score.musicxml", "application/vnd.recordare.musicxml+xml", 200, new string('b', 64)),
                new AssetFile("midi", "score.mid", "audio/midi", 30, new string('c', 64)),
            ],
            new ScoreFacts
            {
                Profile = ScoreProfile.Bass,
                Engine = ScoreEngine.BasicPitch,
                Key = new KeyInfo("A-", KeyMode.Major, 0.83, KeySource.Estimated),
                Bpm = 118,
                TimeSignature = "4/4",
                Bars = 125,
                Notes = 386,
                Pages = 3,
                Resolution = ScoreResolution.Sixteenth,
                Warnings = [new ScoreWarning(ScoreWarningCode.NotesCleaned, "87 very short or out-of-range notes were removed.")],
                Device = new JobDevice(ProcessingDeviceKind.Gpu, "NVIDIA GeForce RTX 5070 Ti", false, null),
            },
            T0.AddMinutes(1));
        return asset;
    }

    [Fact]
    public async Task TheChannelFilter_MatchesTheRecipeSourceNewestFirstAndEscapesQuotes()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (bass1, kick, bass2) = (ScoreOf("bass", 0), ScoreOf("drums.kick", 1), ScoreOf("bass", 2));
        await AddAsync(db, bass1, kick, bass2, Asset.Create(Guid.NewGuid(), "Song - drums", AudioProvenance(), T0));

        await using var read = db.NewContext();
        var assets = new AssetRepository(read);
        (await assets.ListAsync(new AssetQuery { TrackId = TrackId, Channel = "bass" }, Ct)).Items.Select(item => item.Id).ShouldBe([bass2.Id, bass1.Id]);
        (await assets.ListAsync(new AssetQuery { TrackId = TrackId, Channel = "drums.kick" }, Ct)).Items.Select(item => item.Id).ShouldBe([kick.Id]);
        (await assets.ListAsync(new AssetQuery { TrackId = TrackId, Channel = "vocals" }, Ct)).Total.ShouldBe(0);
        (await assets.ListAsync(new AssetQuery { TrackId = TrackId, Channel = "x' OR '1'='1" }, Ct)).Total.ShouldBe(0);
        (await assets.ListAsync(new AssetQuery { TrackId = TrackId }, Ct)).Total.ShouldBe(4);
    }

    private static Asset ScoreOf(string channel, int minutes)
    {
        var provenance = ScoreProvenance();
        var recipe = provenance.ScoreRecipe! with { Source = ScoreSource.FromChannel(TrackId, channel) };
        return Asset.CreateScore(Guid.NewGuid(), $"Song - {channel}", provenance with { ScoreRecipe = recipe }, T0.AddMinutes(minutes));
    }

    private static Provenance ScoreProvenance() => new()
    {
        Origin = AssetOrigin.Transcription,
        TrackId = TrackId,
        TrackTitle = "Song",
        Contents = "bass, sheet music",
        ScoreRecipe = new ScoreRecipe
        {
            Source = ScoreSource.FromChannel(TrackId, "bass"),
            Profile = ScoreProfile.Bass,
            Resolution = ScoreResolution.Sixteenth,
            KeyOverride = "E minor",
            Tempo = new ManualTempo(96, 3, 0.4),
            Range = new ExportRange(30, 75),
        },
    };

    private static Provenance AudioProvenance() => new()
    {
        Origin = AssetOrigin.Export,
        TrackId = TrackId,
        TrackTitle = "Song",
        Contents = "drums, raw",
        Recipe = new ExportRecipe { TrackId = TrackId, Mode = ExportMode.Raw, Channels = ["drums"], Format = new ExportFormat(AudioFormat.Flac, 16, null) },
    };

    private static async Task AddAsync(SqliteTestDatabase db, params Asset[] assets)
    {
        await using var context = db.NewContext();
        var repository = new AssetRepository(context);
        foreach (var asset in assets)
        {
            repository.Add(asset);
        }

        await repository.SaveAsync(Ct);
    }

    private static string Json(object value) => JsonSerializer.Serialize(value);
}
