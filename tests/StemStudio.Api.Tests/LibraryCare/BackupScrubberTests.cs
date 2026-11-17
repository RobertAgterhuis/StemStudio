using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.Assets;
using StemStudio.Domain.LibraryCare;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T031: the scrub of the staged database for excluded categories and incomplete consent (research R-04, FR-003, FR-009).</summary>
public sealed class BackupScrubberTests : ApiTestBase
{
    private static readonly DateTime SnapshotTime = LibraryFixture.T0.AddHours(3);

    private readonly LibraryFixture _library;

    public BackupScrubberTests() => _library = new LibraryFixture(Factory);

    private string Staged => Path.Combine(Factory.DataDir, "library-care", "staging", "scrub", "stemstudio.db");

    [Fact]
    public async Task NothingExcluded_LeavesEveryRow()
    {
        var seeded = await SeedAsync();

        var warnings = await ScrubAsync([]);

        warnings.ShouldBeEmpty();
        await using var context = StagedContext();
        (await context.Tracks.CountAsync(Ct), await context.Assets.CountAsync(Ct), await context.Generations.CountAsync(Ct), await context.VoiceProfiles.CountAsync(Ct))
            .ShouldBe((1, 2, 2, 1));
        (await context.Tracks.SingleAsync(Ct)).DetailPhase!.FilesRemovedAt.ShouldBeNull();
        seeded.Track.ShouldNotBeNull();
    }

    [Fact]
    public async Task DetailStemsExcluded_MarkTheFilesRemovedAtTheSnapshotTime()
    {
        await SeedAsync();

        await ScrubAsync([BackupCategory.DetailStems]);

        await using var context = StagedContext();
        var phase = (await context.Tracks.SingleAsync(Ct)).DetailPhase!;
        (phase.FilesRemovedAt, phase.Parents.Count).ShouldBe((SnapshotTime, 6));
    }

    [Fact]
    public async Task ExportsExcluded_RemoveOnlyExportItems()
    {
        var seeded = await SeedAsync();

        await ScrubAsync([BackupCategory.Exports]);

        await using var context = StagedContext();
        (await context.Assets.Select(asset => asset.Id).ToListAsync(Ct)).ShouldBe([seeded.Generated.Id]);
    }

    [Fact]
    public async Task GenerationsExcluded_RemoveGenerationsAndGeneratedItemsButKeepTracks()
    {
        var seeded = await SeedAsync();

        await ScrubAsync([BackupCategory.Generations]);

        await using var context = StagedContext();
        (await context.Generations.CountAsync(Ct), await context.Tracks.CountAsync(Ct)).ShouldBe((0, 1));
        (await context.Assets.Select(asset => asset.Id).ToListAsync(Ct)).ShouldBe([seeded.Export.Id]);
    }

    [Fact]
    public async Task RecordingsExcluded_RemoveOnlyRecordings()
    {
        var seeded = await SeedAsync();
        var recordings = new HashSet<Guid> { seeded.Export.Id };

        await ScrubAsync([BackupCategory.Recordings], asset => recordings.Contains(asset.Id) ? BackupCategory.Recordings : AssetCategories.Of(asset));

        await using var context = StagedContext();
        (await context.Assets.Select(asset => asset.Id).ToListAsync(Ct)).ShouldBe([seeded.Generated.Id]);
    }

    [Fact]
    public async Task VoiceProfilesExcluded_RemoveProfilesAndDetachConversions()
    {
        var seeded = await SeedAsync();

        var warnings = await ScrubAsync([BackupCategory.VoiceProfiles]);

        warnings.ShouldBeEmpty();
        await using var context = StagedContext();
        (await context.VoiceProfiles.CountAsync(Ct)).ShouldBe(0);
        var conversion = await context.Generations.SingleAsync(generation => generation.Id == seeded.Conversion.Id, Ct);
        (conversion.VoiceProfileId, conversion.VoiceConversion!.VoiceProfileName).ShouldBe((null, "My voice (profile not in backup)"));
    }

    [Fact]
    public async Task IncompleteConsent_IsScrubbedWithAWarning()
    {
        var seeded = await SeedAsync();
        await Factory.WithScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<StemStudioDbContext>();
            await context.Database.ExecuteSqlRawAsync("UPDATE VoiceProfiles SET Consent = replace(Consent, '\"consent.v1\"', '\"consent.v9\"')", Ct);
            return true;
        });

        var warnings = await ScrubAsync([]);

        var warning = warnings.ShouldHaveSingleItem();
        (warning.Code, warning.Message.Contains("My voice", StringComparison.Ordinal)).ShouldBe((ArchiveWarningCodeDto.VoiceProfileWithoutConsent, true));
        await using var context = StagedContext();
        (await context.VoiceProfiles.CountAsync(Ct)).ShouldBe(0);
        (await context.Generations.SingleAsync(generation => generation.Id == seeded.Conversion.Id, Ct)).VoiceProfileId.ShouldBeNull();
    }

    [Fact]
    public async Task Scrub_EndsWithAnIntegrityCheckedDatabase()
    {
        await SeedAsync();

        await ScrubAsync(BackupCategories.Optional);

        await using var connection = new SqliteConnection($"Data Source={Staged};Pooling=False");
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        (await command.ExecuteScalarAsync(Ct)).ShouldBe("ok");
    }

    private async Task<(Domain.Tracks.Track Track, Asset Export, Asset Generated, Domain.Generations.Generation Conversion)> SeedAsync()
    {
        var track = await _library.TrackAsync();
        var export = await _library.ExportAsync(track);
        var generation = await _library.GenerationAsync(track);
        var generated = await GeneratedAssetAsync(generation);
        var profile = await _library.VoiceProfileAsync();
        var conversion = await _library.ConversionAsync(generation, profile);
        return (track, export, generated, conversion);
    }

    private async Task<Asset> GeneratedAssetAsync(Domain.Generations.Generation generation)
    {
        var id = Guid.NewGuid();
        var hash = await _library.FileAsync("assets/" + LibraryFixture.Id(id) + "/audio.flac", 10);
        var asset = Asset.CreateGenerated(id, new Provenance
        {
            Origin = AssetOrigin.Generation,
            TrackTitle = "Song",
            Contents = "Take 1",
            Generation = new GenerationSource(generation.Id, 1, generation.Kind),
        }, new GeneratedAudio("Take 1", [new AssetFile("audio", "audio.flac", "audio/flac", 10, hash)], new AudioFacts(Domain.Rendering.AudioFormat.Flac, 16, null, 30, -1, 0)), LibraryFixture.T0);
        await _library.AddAsync(asset);
        return asset;
    }

    private async Task<IReadOnlyList<ArchiveWarningDto>> ScrubAsync(IReadOnlyCollection<BackupCategory> excluded, Func<Asset, BackupCategory>? categorize = null)
    {
        var live = $"Data Source={Path.Combine(Factory.DataDir, "stemstudio.db")}";
        await new DatabaseSnapshot(TimeProvider.System, NullLogger.Instance).CreateAsync(live, Staged, Ct);
        var scrubber = categorize is null ? new BackupScrubber() : new BackupScrubber(categorize);
        return await scrubber.ScrubAsync(Staged, excluded, SnapshotTime, Ct);
    }

    private StemStudioDbContext StagedContext() =>
        new(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite($"Data Source={Staged};Pooling=False").Options);
}
