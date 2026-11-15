using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Voices;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>010 T018: lyrics sheets, voice profiles, audio hashes and song/conversion columns (migration AddLyricsAndVoices).</summary>
public sealed class LyricsVoicesPersistenceTests
{
    private const string LastMigrationOf009 = "20261115190101_AddGenerativeStyles";
    private static readonly DateTime T0 = TestData.T0;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LyricsSheets_RoundTripSearchAndRemove()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var sheets = new LyricsSheetRepository(context);
        var older = LyricsSheet.Create(Guid.NewGuid(), new LyricsContent("Rain song", "en", "paper boats"), T0);
        var newer = LyricsSheet.Create(Guid.NewGuid(), new LyricsContent("Zon", "nl", "Dit is mijn lied"), T0.AddHours(1));
        await sheets.AddAsync(older, Ct);
        await sheets.AddAsync(newer, Ct);

        (await sheets.ListAsync(null, Ct)).Select(s => s.Id).ShouldBe([newer.Id, older.Id]);
        (await sheets.ListAsync("BOATS", Ct)).Single().Id.ShouldBe(older.Id);
        (await sheets.ListAsync("zon", Ct)).Single().Id.ShouldBe(newer.Id);
        older.Update(new LyricsContent("Rain", "en", "x"), T0.AddHours(2));
        await sheets.SaveAsync(Ct);
        await using (var read = db.NewContext())
        {
            (await new LyricsSheetRepository(read).FindAsync(older.Id, Ct))!.Title.ShouldBe("Rain");
        }

        await sheets.RemoveAsync(older, Ct);
        (await sheets.FindAsync(older.Id, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task VoiceProfiles_RoundTripTheConsentRecordAndNameIsUnique()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var profile = Profile("My voice");
        await using (var write = db.NewContext())
        {
            await new VoiceProfileRepository(write).AddAsync(profile, Ct);
            await Should.ThrowAsync<DuplicateKeyException>(() => new VoiceProfileRepository(write).AddAsync(Profile("MY VOICE"), Ct));
        }

        await using var read = db.NewContext();
        var profiles = new VoiceProfileRepository(read);
        var loaded = (await profiles.FindAsync(profile.Id, Ct))!;
        loaded.Consent.ShouldBe(profile.Consent);
        (loaded.SampleSource, loaded.SamplePath, loaded.SamplePcmSha256, loaded.SampleFileSha256, loaded.DurationSeconds, loaded.VoicedRatio)
            .ShouldBe((VoiceSampleSource.Uploaded, "voices/p/sample.flac", new string('a', 64), new string('b', 64), 21.4, 0.78));
        (await profiles.IsNameTakenAsync("MY VOICE", Ct)).ShouldBeTrue();
        (await profiles.ListAsync(Ct)).Single().Id.ShouldBe(profile.Id);
        await profiles.RemoveAsync(loaded, Ct);
        (await profiles.ListAsync(Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task AudioHashes_AreIdempotentAndRemovedPerOwner()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var hashes = new AudioHashRepository(context);
        var owner = Guid.NewGuid();

        await hashes.AddAsync(new AudioHash(new string('1', 64), AudioHashKind.Stem, owner), Ct);
        await hashes.AddAsync(new AudioHash(new string('1', 64), AudioHashKind.Stem, owner), Ct);
        await hashes.AddRangeAsync([new AudioHash(new string('2', 64), AudioHashKind.Take, owner), new AudioHash(new string('1', 64), AudioHashKind.Upload, owner)], Ct);

        (await db.ScalarAsync("SELECT COUNT(*) FROM AudioHashes")).ShouldBe("2");
        (await hashes.ContainsAsync(new string('2', 64), Ct)).ShouldBeTrue();
        (await hashes.ContainsAsync(new string('3', 64), Ct)).ShouldBeFalse();
        await hashes.RemoveOwnerAsync(owner, Ct);
        (await db.ScalarAsync("SELECT COUNT(*) FROM AudioHashes")).ShouldBe("0");
    }

    [Fact]
    public async Task SongAndConversionGenerations_RoundTrip()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var song = Generation.CreateSong(Guid.NewGuid(), new SongDetails
        {
            Lyrics = new LyricsSnapshot(Guid.NewGuid(), "This little song", "en", "[verse]\nla"),
            FreeCaption = "pop rock",
            LeadVoice = LeadVoice.Female,
            Delivery = "warm",
            Bpm = 96,
            KeyScale = "D minor",
            TimeSignature = "4",
            DurationSeconds = 60,
            Caption = "pop rock, female lead vocal, warm",
        }, [42], T0);
        var conversion = Generation.CreateVoiceConversion(Guid.NewGuid(), new VoiceConversionDetails
        {
            SourceGenerationId = song.Id, SourceTakeIndex = 1, VoiceProfileId = Guid.NewGuid(), VoiceProfileName = "My voice", PitchShift = -2,
        }, T0);
        conversion.WaitForStems(Guid.NewGuid(), T0);
        await using (var write = db.NewContext())
        {
            await new GenerationRepository(write).AddAsync(song, Ct);
            await new GenerationRepository(write).AddAsync(conversion, Ct);
        }

        await using var read = db.NewContext();
        var generations = new GenerationRepository(read);
        (await generations.FindAsync(song.Id, Ct))!.Song.ShouldBe(song.Song);
        var loaded = (await generations.FindAsync(conversion.Id, Ct))!;
        (loaded.VoiceConversion, loaded.Status).ShouldBe((conversion.VoiceConversion, GenerationState.WaitingForStems));
        (await generations.ListByVoiceProfileAsync(conversion.VoiceConversion!.VoiceProfileId!.Value, Ct)).Single().Id.ShouldBe(conversion.Id);
        (await generations.ListActiveAsync(Ct)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task DatabaseFrom009_MigratesToTheNewTables()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-009-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            await SqliteTestDatabase.MigrateLegacyAsync(connectionString, LastMigrationOf009);

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = db.NewContext();
            read.Database.GetPendingMigrations().ShouldBeEmpty();
            (await db.ScalarAsync("SELECT COUNT(*) FROM LyricsSheets")).ShouldBe("0");
            (await db.ScalarAsync("SELECT COUNT(*) FROM VoiceProfiles")).ShouldBe("0");
            (await db.ScalarAsync("SELECT COUNT(*) FROM AudioHashes")).ShouldBe("0");
            (await new TrackRepository(read).ListAsync(Ct)).Single().Origin.ShouldBeNull();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static VoiceProfile Profile(string name) => VoiceProfile.Create(Guid.NewGuid(), new VoiceProfileDraft(
        name,
        new VoiceConsent(ConsentStatement.Version, ConsentStatement.Sha256, "Pat Example", T0),
        new VoiceSample(VoiceSampleSource.Uploaded, "voices/p/sample.flac", new string('a', 64), new string('b', 64), 21.4, 0.78)), T0);
}
