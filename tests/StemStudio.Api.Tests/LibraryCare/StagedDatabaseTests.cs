using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.LibraryCare.Restore;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T044: checks and migration of the staged database (data-model 3.4 steps 7-9, FR-011, FR-014, FR-016, SC-008, SC-009).</summary>
public sealed class StagedDatabaseTests : ApiTestBase
{
    private readonly LibraryFixture _library;

    public StagedDatabaseTests() => _library = new LibraryFixture(Factory);

    private string Staging => Path.Combine(Factory.DataDir + "-backups", "staging");

    private string Db => Path.Combine(Staging, "stemstudio.db");

    [Fact]
    public async Task CurrentSchema_PassesWithoutMigration_AndReconcilesInterruptedWork()
    {
        var track = await _library.TrackAsync();
        await Factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        var manifest = await StageAsync();

        var result = await Prepare().PrepareAsync(Staging, manifest, new RestoreOptionsDto { Mode = RestoreModeDto.Replace }, Ct);

        result.MigratedFrom.ShouldBeNull();
        await using var context = BackupScrubber.StagedContext(Db);
        var tracks = await context.Tracks.ToListAsync(Ct);
        tracks.Single(t => t.Id == track.Id).Status.ShouldBe(TrackStatus.Completed);
        var interrupted = tracks.Single(t => t.Id != track.Id);
        (interrupted.Status, interrupted.Error).ShouldBe((TrackStatus.Failed, StagedDatabase.InterruptedText));
        result.Warnings.ShouldContain(warning => warning.Contains("interrupted", StringComparison.Ordinal));
    }

    // The older library comes from the committed schema-010 fixture: a database built by the Up migrations of that level
    // (a Down-migrated copy is not structurally what an older StemStudio wrote, so the reference schema check refuses it).
    [Fact]
    public async Task OlderSchema_IsMigratedInStaging()
    {
        Directory.CreateDirectory(Factory.BackupDir);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "backup", "schema-010.zip"), Path.Combine(Factory.BackupDir, "schema-010.zip"));
        var manifest = await ExtractAsync("schema-010.zip");

        var result = await Prepare().PrepareAsync(Staging, manifest, new RestoreOptionsDto { Mode = RestoreModeDto.Replace }, Ct);

        result.MigratedFrom.ShouldBe("20261115200748_AddLyricsAndVoices");
        (await BackupRun.MigrationsAsync(Db, Ct)).ShouldBe(Factory.Services.GetRequiredService<MigrationCatalog>().All);
    }

    [Fact]
    public async Task MigrationHistoryThatDiffersFromTheManifest_IsCorrupt()
    {
        await _library.TrackAsync();
        var manifest = await StageAsync();

        var error = await Should.ThrowAsync<LibraryJobException>(() => Prepare().PrepareAsync(
            Staging, manifest with { Schema = manifest.Schema with { Migrations = ["20261114215330_Initial"] } }, new RestoreOptionsDto { Mode = RestoreModeDto.Replace }, Ct));

        error.Code.ShouldBe(LibraryJobErrorCodeDto.ArchiveCorrupt);
    }

    [Fact]
    public async Task DamagedDatabase_IsCorrupt()
    {
        await _library.TrackAsync();
        var manifest = await StageAsync();
        await File.WriteAllTextAsync(Db, "garbage", Ct);

        (await Should.ThrowAsync<LibraryJobException>(() => Prepare().PrepareAsync(Staging, manifest, new RestoreOptionsDto { Mode = RestoreModeDto.Replace }, Ct)))
            .Code.ShouldBe(LibraryJobErrorCodeDto.ArchiveCorrupt);
    }

    [Fact]
    public async Task StoredPathOutsideTheLayout_IsUnsafe()
    {
        await _library.VoiceProfileAsync();
        var manifest = await StageAsync();
        await using (var context = BackupScrubber.StagedContext(Db))
        {
            await context.Database.ExecuteSqlRawAsync("UPDATE VoiceProfiles SET SamplePath = '../../x'", Ct);
        }

        (await Should.ThrowAsync<LibraryJobException>(() => Prepare().PrepareAsync(Staging, manifest, new RestoreOptionsDto { Mode = RestoreModeDto.Replace }, Ct)))
            .Code.ShouldBe(LibraryJobErrorCodeDto.ArchiveUnsafe);
    }

    // Security review 013 item 1: a converted vocal path is a stored path too (data-model 3.4 step 8, "take/vocal paths").
    [Fact]
    public async Task StoredVocalPathOutsideTheLayout_IsUnsafe()
    {
        var track = await _library.TrackAsync();
        await _library.GenerationAsync(track, _ => "../keys/key-1.xml");
        var manifest = await StageAsync();

        (await Should.ThrowAsync<LibraryJobException>(() => Prepare().PrepareAsync(Staging, manifest, new RestoreOptionsDto { Mode = RestoreModeDto.Replace }, Ct)))
            .Code.ShouldBe(LibraryJobErrorCodeDto.ArchiveUnsafe);
    }

    // Security review 013 item 2: SQL objects of the archive (triggers, views, virtual or unknown tables and indexes) would run
    // or hide data on later writes; only the tables and indexes the known migrations create are accepted.
    [Theory]
    [InlineData("CREATE TRIGGER evil AFTER UPDATE ON Tracks BEGIN UPDATE VoiceProfiles SET SamplePath = '../../x'; END")]
    [InlineData("CREATE VIEW evil AS SELECT 1")]
    [InlineData("  /* x */ create /* y */ VIRTUAL table evil USING fts5(a)")]
    [InlineData("CREATE VIRTUAL TABLE Styles2 USING fts5(a)")]
    [InlineData("CrEaTe TrIgGeR evil AFTER INSERT ON Tracks BEGIN SELECT 1; END")]
    [InlineData("cReAtE tRiGgEr \"evil\" before delete on Tracks begin select 1; end")]
    [InlineData("CREATE TABLE Extra (Id INTEGER PRIMARY KEY)")]
    [InlineData("CREATE INDEX IX_Evil ON Tracks (Title)")]
    [InlineData("ALTER TABLE Tracks ADD COLUMN Evil TEXT GENERATED ALWAYS AS (upper(Title)) VIRTUAL")]
    [InlineData("ALTER TABLE Tracks ADD COLUMN Evil TEXT DEFAULT 'x'")]
    [InlineData("DROP INDEX IX_Tracks_UpdatedAt; CREATE INDEX IX_Tracks_UpdatedAt ON Tracks (lower(Title))")]
    [InlineData("DROP INDEX IX_Tracks_UpdatedAt; CREATE INDEX IX_Tracks_UpdatedAt ON Tracks (UpdatedAt) WHERE length(Title) > 1")]
    [InlineData("DROP INDEX IX_Tracks_SourceKey; CREATE INDEX IX_Tracks_SourceKey ON Tracks (SourceKey)")]
    [InlineData("CREATE TABLE h2 AS SELECT * FROM __EFMigrationsHistory; DROP TABLE __EFMigrationsHistory; "
        + "CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, "
        + "\"ProductVersion\" TEXT NOT NULL CHECK (length(ProductVersion) > 0)); INSERT INTO __EFMigrationsHistory SELECT * FROM h2; DROP TABLE h2")]
    public async Task ArchivedTriggerOrView_IsUnsafe(string sql)
    {
        await _library.TrackAsync();
        var manifest = await StageAsync();
        await using (var context = BackupScrubber.StagedContext(Db))
        {
            await context.Database.ExecuteSqlRawAsync(sql, Ct);
        }

        (await Should.ThrowAsync<LibraryJobException>(() => Prepare().PrepareAsync(Staging, manifest, new RestoreOptionsDto { Mode = RestoreModeDto.Replace }, Ct)))
            .Code.ShouldBe(LibraryJobErrorCodeDto.ArchiveUnsafe);
    }

    [Fact]
    public async Task InvalidConsentProof_DropsTheProfileAndDetachesItsConversions()
    {
        var track = await _library.TrackAsync();
        var generation = await _library.GenerationAsync(track);
        var profile = await _library.VoiceProfileAsync();
        var conversion = await _library.ConversionAsync(generation, profile);
        var manifest = await StageAsync();
        var proof = manifest.VoiceProfiles.Single();
        manifest = manifest with { VoiceProfiles = [proof with { Consent = proof.Consent with { StatementSha256 = new string('0', 64) } }] };

        var result = await Prepare().PrepareAsync(Staging, manifest, new RestoreOptionsDto { Mode = RestoreModeDto.Replace }, Ct);

        var skipped = result.Skipped.ShouldHaveSingleItem();
        (skipped.Category, skipped.Id, skipped.Name, skipped.Reason).ShouldBe(("voiceProfiles", profile.Id.ToString("D"), "My voice", SkippedItemReasonDto.ConsentInvalid));
        await using var context = BackupScrubber.StagedContext(Db);
        (await context.VoiceProfiles.CountAsync(Ct)).ShouldBe(0);
        var detached = await context.Generations.SingleAsync(g => g.Id == conversion.Id, Ct);
        detached.VoiceConversion!.VoiceProfileName.ShouldBe("My voice" + Generation.ProfileNotRestoredSuffix);
        File.Exists(Path.Combine(Staging, "files", "voices", profile.Id.ToString("D"), "sample.flac")).ShouldBeFalse();
    }

    [Fact]
    public async Task VoiceProfilesOff_DropsEveryProfileWithReasonOption()
    {
        await _library.VoiceProfileAsync();
        var manifest = await StageAsync();

        var result = await Prepare().PrepareAsync(Staging, manifest, new RestoreOptionsDto { Mode = RestoreModeDto.Replace, VoiceProfiles = false }, Ct);

        result.Skipped.ShouldHaveSingleItem().Reason.ShouldBe(SkippedItemReasonDto.Option);
    }

    [Fact]
    public async Task ValidConsentProof_KeepsTheProfile()
    {
        await _library.VoiceProfileAsync();
        var manifest = await StageAsync();

        var result = await Prepare().PrepareAsync(Staging, manifest, new RestoreOptionsDto { Mode = RestoreModeDto.Replace }, Ct);

        result.Skipped.ShouldBeEmpty();
        await using var context = BackupScrubber.StagedContext(Db);
        (await context.VoiceProfiles.CountAsync(Ct)).ShouldBe(1);
    }

    private StagedDatabase Prepare() => new(Factory.Services.GetRequiredService<MigrationCatalog>(), Factory.Clock);

    /// <summary>Backs up the library and extracts the archive into the staging folder.</summary>
    private async Task<BackupManifest> StageAsync()
    {
        var outcome = await Factory.WithScopeAsync(sp => sp.GetRequiredService<BackupService>().StartAsync(new BackupOptionsDto(), Ct));
        var runner = Factory.Services.GetRequiredService<LibraryJobRunner>();
        await runner.WhenIdleAsync();
        var name = (await runner.GetAsync(outcome.Job!.JobId, Ct))!.Report.ShouldBeOfType<BackupReportDto>().FileName;
        return await ExtractAsync(name);
    }

    private async Task<BackupManifest> ExtractAsync(string name)
    {
        using var archive = Factory.Services.GetRequiredService<ArchiveValidator>().Validate(name);
        await new StagingExtractor(new LibraryCareOptions(), TimeProvider.System).ExtractAsync(archive, Staging, null, Ct);
        return archive.Manifest;
    }
}
