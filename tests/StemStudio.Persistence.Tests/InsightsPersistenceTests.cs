using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Insights;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>
/// Feature 020 T020: the quality report JSON column (migration AddQualityReport), the <c>PracticeNotes</c> table with cascade and
/// the handout item columns (migration AddPracticeNotes) (data-model 5.1, 5.2, 5.4).
/// </summary>
public sealed class InsightsPersistenceTests
{
    private const string LastMigrationBefore020 = "20261118055631_AddTrackAnalyses";
    private const string QualityJobId = "0d1e2f3a4b5c4d6e8f9a0b1c2d3e4f5a";
    private static readonly DateTime T0 = TestData.T0;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_ApplyOnAPre020Database_AndKeepItsRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-020-{Guid.NewGuid():N}.db");
        try
        {
            var connectionString = $"Data Source={path};Pooling=False";
            await SqliteTestDatabase.MigrateLegacyAsync(connectionString, LastMigrationBefore020);
            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = db.NewContext();

            read.Database.GetPendingMigrations().ShouldBeEmpty();
            read.Database.GetAppliedMigrations().ShouldContain(id => id.EndsWith("_AddQualityReport", StringComparison.Ordinal));
            read.Database.GetAppliedMigrations().ShouldContain(id => id.EndsWith("_AddPracticeNotes", StringComparison.Ordinal));
            var legacy = (await new TrackRepository(read).ListAsync(Ct)).Single();
            (legacy.Title, legacy.QualityReport).ShouldBe(("Song", (QualityReport?)null));
            (await db.ScalarAsync("SELECT COUNT(*) FROM PracticeNotes")).ShouldBe("0");
            (await db.ScalarAsync("SELECT COUNT(*) FROM Assets WHERE Handout IS NOT NULL")).ShouldBe("0");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AReadyQualityReport_RoundTripsWithStemsAndPreviousRatings()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        Measure(track, "v1", [Stem("vocals", QualityRating.Poor)]);
        Measure(track, "v2", [Stem("vocals", QualityRating.Fair), Stem("drums.kick", QualityRating.Good)]);

        var loaded = await db.SaveAndReloadAsync(track);

        var report = loaded.QualityReport!;
        (report.Status, report.Progress, report.AnalyzerVersion, report.StemsVersion, report.WorkerJobId).ShouldBe((QualityReportStatus.Ready, 100, "1.0.0", "v2", (string?)null));
        report.MeasuredAt.ShouldBe(T0.AddMinutes(20));
        Json(report.Stems).ShouldBe(Json(track.QualityReport!.Stems));
        report.Stems[1].Artefacts.Kinds.ShouldBe([ArtefactKind.Flicker]);
        report.Previous.ShouldBe(new Dictionary<string, QualityRating> { ["vocals"] = QualityRating.Poor });
        (await db.ScalarAsync("SELECT json_extract(QualityReport, '$.Status') FROM Tracks")).ShouldBe("Ready");
    }

    [Fact]
    public async Task PracticeNotes_RoundTrip_AndAreDeletedWithTheirTrack()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);
        await using (var write = db.NewContext())
        {
            var notes = new PracticeNoteRepository(write);
            notes.Add(PracticeNote.Create(track.Id, HandoutFamily.Bass, "Keep the eighths even.\nMute the open A.", T0));
            notes.Add(PracticeNote.Create(track.Id, HandoutFamily.Overview, "Count in four.", T0.AddMinutes(1)));
            await notes.SaveAsync(Ct);
        }

        await using (var read = db.NewContext())
        {
            var notes = await new PracticeNoteRepository(read).ListAsync(track.Id, Ct);
            notes.Select(note => (note.Family, note.Text)).ShouldBe([(HandoutFamily.Overview, "Count in four."), (HandoutFamily.Bass, "Keep the eighths even.\nMute the open A.")]);
            (await db.ScalarAsync("SELECT Family FROM PracticeNotes ORDER BY UpdatedAt LIMIT 1")).ShouldBe("Bass");
        }

        await using (var delete = db.NewContext())
        {
            var tracks = new TrackRepository(delete);
            await tracks.RemoveAsync((await tracks.FindAsync(track.Id, Ct))!, Ct);
        }

        (await db.ScalarAsync("SELECT COUNT(*) FROM PracticeNotes")).ShouldBe("0");
    }

    [Fact]
    public async Task APracticeNote_IsUpdatedAndRemovedThroughTheRepository()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);
        await using (var write = db.NewContext())
        {
            var notes = new PracticeNoteRepository(write);
            notes.Add(PracticeNote.Create(track.Id, HandoutFamily.Drums, "Ghost notes.", T0));
            await notes.SaveAsync(Ct);
        }

        await using (var update = db.NewContext())
        {
            var notes = new PracticeNoteRepository(update);
            (await notes.FindAsync(track.Id, HandoutFamily.Drums, Ct))!.Update("Ghost notes softer.", T0.AddHours(1));
            (await notes.FindAsync(track.Id, HandoutFamily.Bass, Ct)).ShouldBeNull();
            await notes.SaveAsync(Ct);
        }

        (await db.ScalarAsync("SELECT Text FROM PracticeNotes")).ShouldBe("Ghost notes softer.");
        await using (var remove = db.NewContext())
        {
            var notes = new PracticeNoteRepository(remove);
            notes.Remove((await notes.FindAsync(track.Id, HandoutFamily.Drums, Ct))!);
            await notes.SaveAsync(Ct);
        }

        (await db.ScalarAsync("SELECT COUNT(*) FROM PracticeNotes")).ShouldBe("0");
    }

    [Fact]
    public async Task AHandoutItem_RoundTripsItsRecipeAndFacts_AndTheKindFilterFindsIt()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var handout = ReadyHandout();
        var audio = Asset.Create(Guid.NewGuid(), "Song - drums", AudioProvenance(), T0);
        await using (var write = db.NewContext())
        {
            var assets = new AssetRepository(write);
            assets.Add(handout);
            assets.Add(audio);
            await assets.SaveAsync(Ct);
        }

        await using var read = db.NewContext();
        var repository = new AssetRepository(read);
        var loaded = (await repository.ListAsync(new AssetQuery { Kind = AssetKind.Handout }, Ct)).Items.Single();
        (loaded.Id, loaded.Kind, loaded.Provenance.Origin, loaded.Provenance.Recipe, loaded.Provenance.ScoreRecipe).ShouldBe((handout.Id, AssetKind.Handout, AssetOrigin.Handout, null, null));
        Json(loaded.Provenance.HandoutRecipe!).ShouldBe(Json(handout.Provenance.HandoutRecipe!));
        Json(loaded.Handout!).ShouldBe(Json(handout.Handout!));
        loaded.Handout!.Omitted.ShouldBe([new OmittedIngredient(IngredientCode.ChordsOverLyrics, OmissionReason.NotAvailable)]);
        loaded.Files.Single().FileName.ShouldBe("handout.pdf");
        (await repository.FindAsync(audio.Id, Ct))!.Handout.ShouldBeNull();
        (await db.ScalarAsync($"SELECT json_extract(Provenance, '$.HandoutRecipe.Family') FROM Assets WHERE Kind = 'Handout'")).ShouldBe("Bass");
    }

    private static void Measure(Track track, string stemsVersion, IReadOnlyList<StemQuality> stems)
    {
        track.RequestQuality(stemsVersion, T0.AddMinutes(10));
        track.AssignQualityJob(QualityJobId, T0.AddMinutes(10));
        track.ApplyQualityProgress(true, 0.5, T0.AddMinutes(11));
        track.CompleteQuality("1.0.0", stems, T0.AddMinutes(20));
    }

    private static StemQuality Stem(string channel, QualityRating rating) => new(
        channel,
        rating,
        rating == QualityRating.Good ? QualityReason.Clean : QualityReason.Bleed,
        182.4,
        0.69,
        -6.1,
        new BleedEstimate(QualityLevel.Medium, 0.18, "drums", false, Reliability.Indicative),
        new ArtefactEstimate(QualityLevel.Low, 0.21, channel.Contains('.', StringComparison.Ordinal) ? [ArtefactKind.Flicker] : [], Reliability.Indicative));

    private static Asset ReadyHandout()
    {
        var trackId = Guid.NewGuid();
        var recipe = new HandoutRecipe { Kind = HandoutKind.Song, TrackId = trackId, Family = HandoutFamily.Bass, Paper = Paper.A4, Include = [IngredientCode.Facts, IngredientCode.Notation] };
        var provenance = new Provenance { Origin = AssetOrigin.Handout, TrackId = trackId, TrackTitle = "Example Song", Contents = "Bass handout", HandoutRecipe = recipe, SourceAssetIds = [Guid.NewGuid()] };
        var plan = new HandoutFacts
        {
            Kind = HandoutKind.Song,
            Family = HandoutFamily.Bass,
            Paper = Paper.A4,
            Songs = 1,
            Included = [IngredientCode.Facts, IngredientCode.Notation],
            Omitted = [new OmittedIngredient(IngredientCode.ChordsOverLyrics, OmissionReason.NotAvailable)],
        };
        var asset = Asset.CreateHandout(Guid.NewGuid(), "Example Song - Bass handout", provenance, plan, T0);
        asset.AssignJob("1e2f3a4b5c6d4e7f8a9b0c1d2e3f4a5b", T0);
        asset.CompleteHandout(
            [new AssetFile("pdf", Asset.HandoutFileName, "application/pdf", 184211, new string('a', 64))],
            5,
            ["Example Song", "Form"],
            [new HandoutWarning(HandoutWarningCode.NotationScaled, "Bass - score was scaled to Letter.")],
            T0.AddMinutes(1));
        return asset;
    }

    private static Provenance AudioProvenance() => new()
    {
        Origin = AssetOrigin.Export,
        TrackId = Guid.NewGuid(),
        TrackTitle = "Song",
        Contents = "Drums",
        Recipe = new Domain.Rendering.ExportRecipe
        {
            TrackId = Guid.NewGuid(),
            Mode = Domain.Rendering.ExportMode.Raw,
            Channels = ["drums"],
            Format = new Domain.Rendering.ExportFormat(Domain.Rendering.AudioFormat.Wav, 16, null),
        },
    };

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
}
