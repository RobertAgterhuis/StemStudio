using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Practice;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Setlists;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Sections, score views, setlists and the practice log in SQLite (014 data-model 3.1-3.9, migration AddPracticeFlow).</summary>
public sealed class PracticeFlowPersistenceTests
{
    private const string LastMigrationBefore014 = "20261117151217_AddAgentMessageLyricsId";
    private const string JobId = "0123456789abcdef0123456789abcdef";
    private static readonly DateTime T0 = TestData.T0;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_AppliesOnAPre014Database_AndSeedsTheLogSettings()
    {
        await WithFileDatabaseAsync(async connectionString =>
        {
            await SqliteTestDatabase.MigrateLegacyAsync(connectionString, LastMigrationBefore014);

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = db.NewContext();
            read.Database.GetPendingMigrations().ShouldBeEmpty();
            read.Database.GetAppliedMigrations().ShouldContain(id => id.EndsWith("_AddPracticeFlow", StringComparison.Ordinal));
            var settings = await new PracticeRepository(read).GetSettingsAsync(Ct);
            (settings.Enabled, settings.NoticeAcknowledged, settings.UpdatedAt).ShouldBe((true, false, (DateTime?)null));
            (await db.ScalarAsync("SELECT COUNT(*) FROM TrackSections")).ShouldBe("0");
        });
    }

    [Fact]
    public async Task SectionSet_RoundTripsWithSectionsWarningsAndEnums()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        var set = DetectedSet(track.Id);
        await using (var write = db.NewContext())
        {
            await new SectionRepository(write).AddAsync(set, Ct);
        }

        await using var read = db.NewContext();
        var loaded = (await new SectionRepository(read).FindAsync(track.Id, Ct))!;

        (loaded.Status, loaded.Revision, loaded.Analyzer, loaded.Snap, loaded.Origin).ShouldBe((SectionSetStatus.Ready, 1, "stems-novelty/1.0.0", (SectionSnap?)SectionSnap.Downbeat, SectionSetOrigin.Detected));
        loaded.Warnings.ShouldBe([new SectionWarning("grid-unreliable", "The beat grid is unreliable.")]);
        loaded.Sections.Select(section => (section.Position, section.Kind, section.Label, section.Instrument, section.Confidence)).ShouldBe(
            [(0, SectionKind.Intro, "Intro", (string?)null, (double?)0.8), (1, SectionKind.Solo, "Guitar solo", "guitar", 0.6)]);
        (await db.ScalarAsync("SELECT Kind FROM Sections WHERE Position = 1")).ShouldBe("Solo");
    }

    [Fact]
    public async Task SectionSet_ReplaceKeepsIds_AndDropsRemovedRows()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        await using (var write = db.NewContext())
        {
            await new SectionRepository(write).AddAsync(DetectedSet(track.Id), Ct);
        }

        Guid kept;
        await using (var edit = db.NewContext())
        {
            var repository = new SectionRepository(edit);
            var set = (await repository.FindAsync(track.Id, Ct))!;
            kept = set.Sections[0].Id;
            var context = new SectionContext(200, SectionChannels.Of(track));
            set.Replace(new SectionSetUpdate(1, [new SectionInput(kept, SectionKind.Intro, "Start", 0, 8), new SectionInput(null, SectionKind.Verse, "Verse", 8, 30)]), context, Guid.NewGuid, T0.AddHours(1));
            await repository.SaveAsync(Ct);
        }

        await using var read = db.NewContext();
        var loaded = (await new SectionRepository(read).FindAsync(track.Id, Ct))!;
        loaded.Sections.Select(section => section.Label).ShouldBe(["Start", "Verse"]);
        loaded.Sections[0].Id.ShouldBe(kept);
        (loaded.Origin, loaded.Revision).ShouldBe((SectionSetOrigin.Edited, 2));
        (await db.ScalarAsync("SELECT COUNT(*) FROM Sections")).ShouldBe("2");
    }

    [Fact]
    public async Task ActiveSectionSets_AreTheQueuedAndDetectingOnes()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (first, second) = (await SeedTrackAsync(db), await SeedTrackAsync(db, "aaaaaaaaaaa"));
        var queued = TrackSections.Create(first.Id, T0);
        queued.StartDetection(JobId, T0);
        await using (var write = db.NewContext())
        {
            await new SectionRepository(write).AddAsync(queued, Ct);
            await new SectionRepository(write).AddAsync(DetectedSet(second.Id), Ct);
        }

        await using var read = db.NewContext();
        (await new SectionRepository(read).ActiveAsync(Ct)).Select(set => set.TrackId).ShouldBe([first.Id]);
    }

    [Fact]
    public async Task ScoreView_RoundTripsBars_AndIsDeletedWithItsAsset()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        var asset = await SeedAssetAsync(db, track.Id);
        var view = ScoreView.Create(asset.Id, track.Id, 6.1, T0);
        view.Submit(JobId, T0);
        view.Complete(1, [new ScoreBar { Index = 0, Number = 0, Page = 1, X = 0.05, Y = 0.1, Width = 0.07, Height = 0.02, StartSeconds = 0, EndSeconds = 2 }], T0);
        await using (var write = db.NewContext())
        {
            await new ScoreViewRepository(write).AddAsync(view, Ct);
        }

        await using (var read = db.NewContext())
        {
            var loaded = (await new ScoreViewRepository(read).FindAsync(asset.Id, Ct))!;
            (loaded.Status, loaded.PageCount, loaded.Revision, loaded.OffsetSeconds, loaded.TrackId).ShouldBe((ScoreViewStatus.Ready, 1, 1, 6.1, (Guid?)track.Id));
            loaded.Bars.Single().StartSeconds.ShouldBe(6.1);
            (await new ScoreViewRepository(read).ActiveAsync(Ct)).ShouldBeEmpty();
        }

        await DeleteAssetAsync(db, asset.Id);
        (await db.ScalarAsync("SELECT COUNT(*) FROM ScoreViews")).ShouldBe("0");
    }

    [Fact]
    public async Task Setlist_RoundTripsEntriesInOrder()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (track, mix) = await SeedTrackWithMixAsync(db);
        var setlist = await SeedSetlistAsync(db, track.Id, mix.Id);

        await using var read = db.NewContext();
        var repository = new SetlistRepository(read);
        var loaded = (await repository.FindAsync(setlist.Id, Ct))!;
        loaded.Entries.Select(entry => (entry.Position, entry.TrackId, entry.MixId, entry.Note)).ShouldBe([(0, track.Id, (Guid?)mix.Id, "Start soft."), (1, track.Id, null, "Encore")]);
        (await repository.ListAsync(Ct)).Single().Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MixDelete_ClearsTheEntryMix_AndTheSessionMix()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (track, mix) = await SeedTrackWithMixAsync(db);
        var setlist = await SeedSetlistAsync(db, track.Id, mix.Id);
        var session = await SeedSessionAsync(db, track.Id, mix.Id);
        await using (var context = db.NewContext())
        {
            var repository = new MixRepository(context);
            await repository.RemoveAsync((await repository.FindAsync(mix.Id, Ct))!, Ct);
        }

        await using var read = db.NewContext();
        (await new SetlistRepository(read).FindAsync(setlist.Id, Ct))!.Entries[0].MixId.ShouldBeNull();
        (await new PracticeRepository(read).FindSessionAsync(session.Id, Ct))!.MixId.ShouldBeNull();
    }

    [Fact]
    public async Task TrackDelete_CascadesSectionsEntriesAndSessions_AndCountsRemovedEntries()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (track, mix) = await SeedTrackWithMixAsync(db);
        var other = await SeedTrackAsync(db, "bbbbbbbbbbb");
        await using (var write = db.NewContext())
        {
            await new SectionRepository(write).AddAsync(DetectedSet(track.Id), Ct);
        }

        var setlist = await SeedSetlistAsync(db, track.Id, mix.Id, other.Id);
        await SeedSessionAsync(db, track.Id, null);
        await using (var context = db.NewContext())
        {
            var repository = new TrackRepository(context);
            await repository.RemoveWithMixesAsync((await repository.FindAsync(track.Id, Ct))!, Ct);
        }

        foreach (var table in new[] { "TrackSections", "Sections", "PracticeSessions", "PracticeSectionTimes" })
        {
            (await db.ScalarAsync($"SELECT COUNT(*) FROM {table}")).ShouldBe("0");
        }

        await using var read = db.NewContext();
        var loaded = (await new SetlistRepository(read).FindAsync(setlist.Id, Ct))!;
        loaded.Entries.Select(entry => entry.TrackId).ShouldBe([other.Id]);
        loaded.RemovedEntries.ShouldBe(2);
    }

    [Fact]
    public async Task PlainTrackRemove_AlsoCountsRemovedEntries()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        var setlist = await SeedSetlistAsync(db, track.Id, null);
        await using (var context = db.NewContext())
        {
            var repository = new TrackRepository(context);
            await repository.RemoveAsync((await repository.FindAsync(track.Id, Ct))!, Ct);
        }

        await using var read = db.NewContext();
        (await new SetlistRepository(read).FindAsync(setlist.Id, Ct))!.RemovedEntries.ShouldBe(2);
    }

    [Fact]
    public async Task SetlistDelete_LeavesTracksAndMixes()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (track, mix) = await SeedTrackWithMixAsync(db);
        var setlist = await SeedSetlistAsync(db, track.Id, mix.Id);
        await using (var context = db.NewContext())
        {
            var repository = new SetlistRepository(context);
            await repository.RemoveAsync((await repository.FindAsync(setlist.Id, Ct))!, Ct);
        }

        (await db.ScalarAsync("SELECT COUNT(*) FROM SetlistEntries")).ShouldBe("0");
        (await db.ScalarAsync("SELECT COUNT(*) FROM Mixes")).ShouldBe("1");
        (await db.ScalarAsync("SELECT COUNT(*) FROM Tracks")).ShouldBe("1");
    }

    [Fact]
    public async Task Session_UpsertReplacesItsSectionRows()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        var session = await SeedSessionAsync(db, track.Id, null);
        await using (var context = db.NewContext())
        {
            var repository = new PracticeRepository(context);
            var tracked = (await repository.FindSessionAsync(session.Id, Ct))!;
            tracked.Replace(SessionInput(track.Id, null, 900, Section("Chorus", 400)), T0.AddMinutes(20));
            await repository.SaveAsync(Ct);
        }

        await using var read = db.NewContext();
        var loaded = (await new PracticeRepository(read).FindSessionAsync(session.Id, Ct))!;
        loaded.PlayedSeconds.ShouldBe(900);
        loaded.Sections.Select(row => (row.Index, row.Label, row.Kind)).ShouldBe([(0, "Chorus", PracticeSectionKind.Chorus)]);
        (await db.ScalarAsync("SELECT COUNT(*) FROM PracticeSectionTimes")).ShouldBe("1");
    }

    [Fact]
    public async Task Sessions_ListPageDeleteAndCount()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (first, second) = (await SeedTrackAsync(db), await SeedTrackAsync(db, "ccccccccccc"));
        var older = await SeedSessionAsync(db, first.Id, null, T0);
        var newer = await SeedSessionAsync(db, first.Id, null, T0.AddHours(1));
        await SeedSessionAsync(db, second.Id, null, T0.AddHours(2));

        await using var context = db.NewContext();
        var repository = new PracticeRepository(context);
        (await repository.ListSessionsAsync(null, null, 50, Ct)).Count.ShouldBe(3);
        (await repository.ListSessionsAsync(first.Id, null, 50, Ct)).Select(session => session.Id).ShouldBe([newer.Id, older.Id]);
        (await repository.ListSessionsAsync(first.Id, T0.AddHours(1), 50, Ct)).Select(session => session.Id).ShouldBe([older.Id]);
        (await repository.ListSessionsAsync(null, null, 1, Ct)).Count.ShouldBe(1);
        (await repository.SessionsOfTrackAsync(first.Id, Ct)).Count.ShouldBe(2);
        (await repository.AllSessionsAsync(Ct)).Count.ShouldBe(3);
        (await repository.CountSessionsAsync(Ct)).ShouldBe(3);
        (await repository.DeleteSessionsAsync(first.Id, Ct)).ShouldBe(2);
        (await repository.DeleteSessionsAsync(null, Ct)).ShouldBe(1);
        (await db.ScalarAsync("SELECT COUNT(*) FROM PracticeSectionTimes")).ShouldBe("0");
    }

    [Fact]
    public async Task OneSession_IsRemoved_AndSettingsAreSaved()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = await SeedTrackAsync(db);
        var session = await SeedSessionAsync(db, track.Id, null);
        await using (var context = db.NewContext())
        {
            var repository = new PracticeRepository(context);
            await repository.RemoveSessionAsync((await repository.FindSessionAsync(session.Id, Ct))!, Ct);
            var settings = await repository.GetSettingsAsync(Ct);
            settings.Update(false, true, T0);
            await repository.SaveAsync(Ct);
        }

        await using var read = db.NewContext();
        var reloaded = new PracticeRepository(read);
        (await reloaded.FindSessionAsync(session.Id, Ct)).ShouldBeNull();
        var stored = await reloaded.GetSettingsAsync(Ct);
        (stored.Enabled, stored.NoticeAcknowledged, stored.UpdatedAt).ShouldBe((false, true, (DateTime?)T0));
    }

    [Fact]
    public async Task MissingSettingsRow_IsRecreatedWithTheDefaults()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await db.ScalarAsync("DELETE FROM PracticeLogSettings");

        await using var context = db.NewContext();
        (await new PracticeRepository(context).GetSettingsAsync(Ct)).Enabled.ShouldBeTrue();
        (await db.ScalarAsync("SELECT COUNT(*) FROM PracticeLogSettings")).ShouldBe("1");
    }

    [Fact]
    public async Task MixLoopJson_WithASectionIdKey_StillLoads()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (_, mix) = await SeedTrackWithMixAsync(db);
        await db.ScalarAsync("""UPDATE Mixes SET Loop = '{"Start":10,"End":20,"Enabled":true,"SectionId":"0b1c2d3e-0000-4000-8000-000000000004"}'""");

        await using var read = db.NewContext();
        (await new MixRepository(read).FindAsync(mix.Id, Ct))!.Settings.Loop.ShouldBe(new LoopRegion(10, 20, true) { SectionId = Guid.Parse("0b1c2d3e-0000-4000-8000-000000000004") });
    }

    internal static TrackSections DetectedSet(Guid trackId)
    {
        var set = TrackSections.Create(trackId, T0);
        set.ApplyDetected(
            new DetectionResult("stems-novelty/1.0.0", SectionSnap.Downbeat, [new SectionWarning("grid-unreliable", "The beat grid is unreliable.")], [
                new DetectedSection(SectionKind.Intro, "Intro", 0, 8, null, 0.8),
                new DetectedSection(SectionKind.Solo, "Guitar solo", 8, 30, "guitar", 0.6),
            ]),
            Guid.NewGuid,
            T0);
        return set;
    }

    private static PracticeSessionInput SessionInput(Guid trackId, Guid? mixId, double played, params PracticeSectionInput[] sections) => new()
    {
        TrackId = trackId,
        MixId = mixId,
        StartedAt = T0,
        EndedAt = T0.AddMinutes(20),
        PlayedSeconds = played,
        LoopPasses = 2,
        Sections = sections,
    };

    private static PracticeSectionInput Section(string label, double played) =>
        new() { SectionId = Guid.NewGuid(), Kind = PracticeSectionKind.Chorus, Label = label, PlayedSeconds = played, LoopPasses = 1, TrainerReachedPercent = 80 };

    private static async Task<PracticeSession> SeedSessionAsync(SqliteTestDatabase db, Guid trackId, Guid? mixId, DateTime? start = null)
    {
        var input = SessionInput(trackId, mixId, 600, Section("Solo", 300), Section("Verse", 200));
        var at = start ?? T0;
        var session = PracticeSession.Create(Guid.NewGuid(), input with { StartedAt = at, EndedAt = at.AddMinutes(20) }, at.AddMinutes(20));
        await using var context = db.NewContext();
        await new PracticeRepository(context).AddSessionAsync(session, Ct);
        return session;
    }

    private static async Task<Setlist> SeedSetlistAsync(SqliteTestDatabase db, Guid trackId, Guid? mixId, Guid? thirdTrack = null)
    {
        var setlist = Setlist.Create(Guid.NewGuid(), "Friday", "", T0);
        var tracks = new HashSet<Guid> { trackId };
        var entries = new List<SetlistEntryInput> { new(null, trackId, mixId, "Start soft."), new(null, trackId, null, "Encore") };
        if (thirdTrack is { } third)
        {
            tracks.Add(third);
            entries.Add(new SetlistEntryInput(null, third, null, null));
        }

        var mixes = mixId is { } id ? new Dictionary<Guid, Guid> { [id] = trackId } : [];
        setlist.ReplaceEntries(entries, new SetlistEntryContext(tracks, mixes), Guid.NewGuid, T0);
        await using var context = db.NewContext();
        await new SetlistRepository(context).AddAsync(setlist, Ct);
        return setlist;
    }

    private static async Task<Track> SeedTrackAsync(SqliteTestDatabase db, string videoId = "dQw4w9WgXcQ")
    {
        var track = TestData.CompletedTrack(videoId);
        await db.SeedAsync(track);
        return track;
    }

    private static async Task<(Track Track, Mix Mix)> SeedTrackWithMixAsync(SqliteTestDatabase db)
    {
        var track = await SeedTrackAsync(db);
        var mix = Mix.Create(Guid.NewGuid(), track, TestData.Settings(), T0.AddHours(1));
        await using var context = db.NewContext();
        await new MixRepository(context).AddAsync(mix, Ct);
        return (track, mix);
    }

    private static async Task<Asset> SeedAssetAsync(SqliteTestDatabase db, Guid trackId)
    {
        var asset = Asset.Create(Guid.NewGuid(), "Song - bass", new Provenance
        {
            Origin = AssetOrigin.Export,
            TrackId = trackId,
            TrackTitle = "Song",
            Contents = "bass, raw",
            Recipe = new ExportRecipe { TrackId = trackId, Mode = ExportMode.Raw, Channels = ["bass"], Format = new ExportFormat(AudioFormat.Flac, 16, null) },
        }, T0);
        await using var context = db.NewContext();
        var repository = new AssetRepository(context);
        repository.Add(asset);
        await repository.SaveAsync(Ct);
        return asset;
    }

    private static async Task DeleteAssetAsync(SqliteTestDatabase db, Guid assetId)
    {
        await using var context = db.NewContext();
        var repository = new AssetRepository(context);
        await repository.RemoveAsync((await repository.FindAsync(assetId, Ct))!, Ct);
    }

    private static async Task WithFileDatabaseAsync(Func<string, Task> test)
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-014-{Guid.NewGuid():N}.db");
        try
        {
            await test($"Data Source={path};Pooling=False");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
