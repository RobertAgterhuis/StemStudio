using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Styles;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Feature 009 T028: styles, generations and track origins in SQLite (data-model 3.5, migration AddGenerativeStyles).</summary>
public sealed class GenerativePersistenceTests
{
    private const string LastMigrationOf008 = "20261115171827_AddScoreFacts";
    private const string JobId = "5b0f7e2c9a1d4c3e8f6a2b1d0c9e8f7a";
    private static readonly DateTime T0 = TestData.T0;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AUserStyle_RoundTripsItsTags()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var style = Style.CreateUser(Guid.NewGuid(), Fields("Rainy cafe jazz"), T0);

        await using (var write = db.NewContext())
        {
            await new StyleRepository(write).AddAsync(style, Ct);
        }

        await using var read = db.NewContext();
        var loaded = (await new StyleRepository(read).FindAsync(style.Id, Ct))!;
        loaded.Fields.ShouldBe(style.Fields);
        (loaded.Kind, loaded.PresetKey, loaded.CreatedAt).ShouldBe((StyleKind.User, null, T0));
        (await db.ScalarAsync("SELECT Moods FROM Styles")).ShouldBe("""["melancholic","intimate"]""");
    }

    [Fact]
    public async Task StyleNames_AreUniqueIgnoringCase()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var styles = new StyleRepository(context);
        await styles.AddAsync(Style.CreateUser(Guid.NewGuid(), Fields("Rainy"), T0), Ct);

        await Should.ThrowAsync<DuplicateKeyException>(() => styles.AddAsync(Style.CreateUser(Guid.NewGuid(), Fields("RAINY "), T0), Ct));
        (await styles.IsNameTakenAsync("RAINY", null, Ct)).ShouldBeTrue();
        (await styles.IsNameTakenAsync("OTHER", null, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task StyleRepository_ListsNamesAndRemoves()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var styles = new StyleRepository(context);
        var style = Style.CreateUser(Guid.NewGuid(), Fields("Mine"), T0);
        await styles.AddAsync(style, Ct);

        (await styles.IsNameTakenAsync("MINE", style.Id, Ct)).ShouldBeFalse();
        (await styles.NamesAsync(Ct)).ShouldBe(["Mine"]);
        (await styles.ListAsync(Ct)).Single().Id.ShouldBe(style.Id);
        style.Update(Fields("Renamed"), T0.AddHours(1));
        await styles.SaveAsync(Ct);
        await styles.RemoveAsync(style, Ct);
        (await styles.ListAsync(Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SeedPresets_IsIdempotentAndFollowsTheResource()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var presets = PresetCatalog.Load().Presets;

        (await SeedAsync(db, presets, T0)).ShouldBe(presets.Count);
        (await SeedAsync(db, presets, T0.AddDays(1))).ShouldBe(0);
        var changed = presets.Skip(1).Select((p, i) => i == 0 ? p with { Fields = p.Fields with { Moods = ["changed"] } } : p).ToList();
        (await SeedAsync(db, changed, T0.AddDays(2))).ShouldBe(2);

        (await db.ScalarAsync("SELECT COUNT(*) FROM Styles WHERE Kind = 'Preset'")).ShouldBe((presets.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        (await db.ScalarAsync($"SELECT Moods FROM Styles WHERE Id = '{presets[1].Id.ToString().ToUpperInvariant()}'")).ShouldBe("""["changed"]""");
    }

    [Fact]
    public async Task SeedPresets_RenamesAUserStyleThatTakesAPresetName()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var preset = PresetCatalog.Load().Presets[0];
        await using (var write = db.NewContext())
        {
            await new StyleRepository(write).AddAsync(Style.CreateUser(Guid.NewGuid(), Fields(preset.Fields.Name), T0), Ct);
        }

        (await SeedAsync(db, [preset], T0)).ShouldBe(1);

        (await db.ScalarAsync("SELECT Name FROM Styles WHERE Kind = 'User'")).ShouldBe(preset.Fields.Name + " (copy)");
    }

    [Fact]
    public async Task AGeneration_RoundTripsSnapshotsSeedsTakesAndErrors()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var completed = Restyle(TestData.T0);
        completed.AssignJob(JobId, T0);
        completed.ApplyReport(new GenerationReport(GenerationState.Running, 40, "Generating", new JobDevice(ProcessingDeviceKind.Gpu, "RTX", false, null), ["Clipped"]), T0);
        completed.Complete([Take(1) with { LibraryAssetId = Guid.NewGuid() }, Take(2)], T0.AddSeconds(40));
        var failed = Restyle(T0.AddMinutes(1));
        failed.Fail(new GenerationError(GenerationErrorCode.OutOfMemory, "Not enough GPU memory"), T0.AddMinutes(2));

        await AddAsync(db, completed, failed);

        await using var read = db.NewContext();
        var loaded = (await new GenerationRepository(read).FindAsync(completed.Id, Ct))!;
        loaded.Restyle.ShouldBe(completed.Restyle);
        loaded.Seeds.ShouldBe([11, 22]);
        loaded.Takes.ShouldBe(completed.Takes);
        loaded.Warnings.ShouldBe(["Clipped"]);
        loaded.Device!.Name.ShouldBe("RTX");
        (loaded.Status, loaded.CompletedAt, loaded.GeneratorJobId).ShouldBe((GenerationState.Completed, T0.AddSeconds(40), JobId));
        (await new GenerationRepository(read).FindAsync(failed.Id, Ct))!.Error.ShouldBe(new GenerationError(GenerationErrorCode.OutOfMemory, "Not enough GPU memory"));
    }

    [Fact]
    public async Task GenerationRepository_FiltersNewestFirstAndListsActive()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (old, recent, other) = (Restyle(T0), Restyle(T0.AddMinutes(5)), Restyle(T0.AddMinutes(9), Guid.NewGuid()));
        old.Cancel(T0.AddMinutes(1));
        await AddAsync(db, old, recent, other);

        await using var read = db.NewContext();
        var generations = new GenerationRepository(read);
        (await generations.ListAsync(null, null, Ct)).Select(g => g.Id).ShouldBe([other.Id, recent.Id, old.Id]);
        (await generations.ListAsync(GenerationKind.Restyle, TestData.CompletedTrack().Id, Ct)).Count.ShouldBe(0);
        (await generations.ListAsync(GenerationKind.Song, null, Ct)).ShouldBeEmpty();
        (await generations.ListAsync(null, SourceTrack, Ct)).Select(g => g.Id).ShouldBe([recent.Id, old.Id]);
        (await generations.ListActiveAsync(Ct)).Select(g => g.Id).ShouldBe([recent.Id, other.Id], ignoreOrder: true);
        (await generations.ListBySourceTrackAsync(SourceTrack, Ct)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task GenerationRepository_SavesAndRemoves()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var generations = new GenerationRepository(context);
        var generation = Restyle(T0);
        await generations.AddAsync(generation, Ct);

        generation.Cancel(T0.AddSeconds(1));
        await generations.SaveAsync(Ct);
        (await db.ScalarAsync("SELECT Status FROM Generations")).ShouldBe("Cancelled");
        await generations.RemoveAsync(generation, Ct);
        (await db.ScalarAsync("SELECT COUNT(*) FROM Generations")).ShouldBe("0");
    }

    [Fact]
    public async Task TrackOrigin_RoundTrips()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        var origin = new TrackOrigin(Guid.NewGuid(), 2, GenerationKind.Restyle, SourceTrack);
        track.MarkGenerated(origin, T0);

        var loaded = await db.SaveAndReloadAsync(track);

        loaded.Origin.ShouldBe(origin);
    }

    [Fact]
    public async Task DatabaseFrom008_LoadsTracksWithoutOriginAndHasTheNewTables()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-008-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            await SqliteTestDatabase.MigrateLegacyAsync(connectionString, LastMigrationOf008);

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = db.NewContext();
            read.Database.GetPendingMigrations().ShouldBeEmpty();
            (await new TrackRepository(read).ListAsync(Ct)).Single().Origin.ShouldBeNull();
            (await db.ScalarAsync("SELECT COUNT(*) FROM Styles")).ShouldBe("0");
            (await db.ScalarAsync("SELECT COUNT(*) FROM Generations")).ShouldBe("0");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static readonly Guid SourceTrack = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    private static StyleFields Fields(string name) => new()
    {
        Name = name,
        GenreFamily = GenreFamily.Jazz,
        SubGenre = "cool jazz",
        TempoFeel = TempoFeel.Laidback,
        Instrumentation = ["muted trumpet", "piano"],
        Moods = ["melancholic", "intimate"],
        VocalHandling = VocalHandling.Instrumental,
        DefaultStrength = Strength.Light,
        Description = "Late-night trio.",
    };

    private static Generation Restyle(DateTime now, Guid? sourceTrack = null)
    {
        var style = Style.CreateUser(Guid.NewGuid(), Fields("Rainy"), T0);
        var details = new RestyleDetails
        {
            SourceTrackId = sourceTrack ?? SourceTrack,
            SourceTrackTitle = "Song",
            Range = new SourceRange(0, 30, RestyleMode.InPlace),
            IncludedStems = ["drums", "bass"],
            Style = style.Snapshot(),
            Strength = Strength.Light,
            Caption = "Jazz cool jazz",
            AudioCoverStrength = 0.7,
        };
        return Generation.CreateRestyle(Guid.NewGuid(), details, [11, 22], now);
    }

    private static GenerationTake Take(int index) =>
        new(index, index * 11, 30, new string('b', 64), 1234, -1.5, $"generations/g/take-{index}.flac", new TakeModel("acestep-v15-turbo", "19671f4"));

    private static async Task AddAsync(SqliteTestDatabase db, params Generation[] generations)
    {
        await using var context = db.NewContext();
        var repository = new GenerationRepository(context);
        foreach (var generation in generations)
        {
            await repository.AddAsync(generation, Ct);
        }
    }

    private static async Task<int> SeedAsync(SqliteTestDatabase db, IReadOnlyList<PresetDefinition> presets, DateTime now)
    {
        await using var context = db.NewContext();
        return await new StyleRepository(context).SeedPresetsAsync(presets, now, Ct);
    }
}
