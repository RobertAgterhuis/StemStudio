using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Restore;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Domain.Assets;
using StemStudio.Domain.LibraryCare;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Recording;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>
/// Feature 015 T025a: takes in library care (DELTA.md 7 "013 implementation notes"): the stored origin, the scrub of
/// <c>recordings=false</c>, calibrations in the database category, the file selection without <c>capture.wav</c> and the
/// merge restore of takes and calibrations.
/// </summary>
public sealed class RecordingLibraryCareTests : ApiTestBase
{
    private const string FixtureProvenance = "{\"Origin\":\"Recording\",\"TrackId\":\"0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f\",\"TrackTitle\":\"Library care fixture\",\"TrackArtist\":null,\"SourceAssetIds\":[],\"Contents\":\"play-along take 1\"}";

    private readonly LibraryFixture _library;

    public RecordingLibraryCareTests() => _library = new LibraryFixture(Factory);

    private string Staged => Path.Combine(Factory.DataDir, "library-care", "staging", "rec", "stemstudio.db");

    private string LiveConnection => $"Data Source={Path.Combine(Factory.DataDir, "stemstudio.db")}";

    [Fact]
    public async Task Origin_IsStoredAsRecording_AndTheBackupFixtureRowReadsBack()
    {
        var track = await _library.TrackAsync(detail: false);
        var (_, asset) = await _library.TakeAsync(track);
        var fixtureId = Guid.Parse("8f7e6d5c-4b3a-4291-8f0e-1d2c3b4a5968");

        var (stored, read) = await Factory.WithScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<StemStudioDbContext>();
            await context.Database.ExecuteSqlAsync(
                $"INSERT INTO Assets (Id, Kind, Name, Status, Progress, Audio, Provenance, TrackId, SizeBytes, CreatedAt, UpdatedAt, Files, FiledVersion, FolderAlbum, FolderAlbumKey, FolderAlbumSource, FolderArtist, FolderArtistKey, FolderArtistSource, FolderSong, FolderSongKey, FolderSongSource) VALUES ({fixtureId.ToString().ToUpperInvariant()}, 'Audio', 'Library care fixture - take 1', 'Ready', 100, NULL, {FixtureProvenance}, '0B7E4F1C-6A2D-4C8E-9F10-2A3B4C5D6E7F', 4096, '2026-11-16 12:00:00', '2026-11-16 12:00:00', '[]', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)",
                Ct);
            var origin = await context.Database.SqlQuery<string>($"SELECT json_extract(Provenance, '$.Origin') AS Value FROM Assets WHERE Id = {asset.Id.ToString().ToUpperInvariant()}").SingleAsync(Ct);
            var fixture = await context.Assets.AsNoTracking().SingleAsync(item => item.Id == fixtureId, Ct);
            return (origin, fixture);
        });

        stored.ShouldBe("Recording");
        (read.Provenance.Origin, read.Provenance.Recording, AssetCategories.Of(read)).ShouldBe((AssetOrigin.Recording, null, BackupCategory.Recordings));
    }

    [Fact]
    public async Task RecordingsExcluded_ScrubTheTakeRows_AndClearTheMixTakeChannels()
    {
        var track = await _library.TrackAsync(detail: false);
        var (take, _) = await _library.TakeAsync(track);
        var export = await _library.ExportAsync(track);
        await MixWithTakeAsync(track, take.Id);
        await CalibrationAsync("Focusrite");

        await ScrubAsync([BackupCategory.Recordings]);

        await using var context = StagedContext();
        (await context.RecordingTakes.CountAsync(Ct), await context.InputCalibrations.CountAsync(Ct)).ShouldBe((0, 1));
        (await context.Assets.Select(asset => asset.Id).ToListAsync(Ct)).ShouldBe([export.Id]);
        (await context.Mixes.SingleAsync(Ct)).Take.ShouldBeNull();
    }

    [Fact]
    public async Task ExportsExcluded_KeepTakes_AndNothingExcludedKeepsCalibrations()
    {
        var track = await _library.TrackAsync(detail: false);
        var (take, asset) = await _library.TakeAsync(track);
        await _library.ExportAsync(track);
        await MixWithTakeAsync(track, take.Id);
        await CalibrationAsync("Focusrite");

        await ScrubAsync([BackupCategory.Exports]);

        await using var context = StagedContext();
        (await context.RecordingTakes.SingleAsync(Ct)).AssetId.ShouldBe(asset.Id);
        (await context.Mixes.SingleAsync(Ct)).Take!.TakeId.ShouldBe(take.Id);
        (await context.InputCalibrations.SingleAsync(Ct)).DeviceLabel.ShouldBe("Focusrite");
    }

    [Fact]
    public async Task Selection_HasTheReadyTakeAudio_InRecordings_ButNeverTheCaptureFile()
    {
        var track = await _library.TrackAsync(detail: false);
        var (_, asset) = await _library.TakeAsync(track, capture: true);

        await ScrubAsync([]);

        await using var context = StagedContext();
        var plan = await new BackupSelection(Factory.Services.GetRequiredService<StemStudio.Api.Storage.DataDirectory>()).ReadAsync(context, Ct);
        var takeFiles = plan.Files.Where(file => file.Source.EntryName.Contains(LibraryFixture.Id(asset.Id), StringComparison.Ordinal)).ToList();
        takeFiles.ShouldHaveSingleItem().Source.EntryName.ShouldEndWith("/audio.flac");
        takeFiles[0].Source.Category.ShouldBe(BackupCategory.Recordings);
        plan.Files.ShouldNotContain(file => file.Source.EntryName.EndsWith("capture.wav", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Merge_AddsTakesOnlyWithTheirItem_CalibrationsByLabel_LiveWins()
    {
        var track = await _library.TrackAsync(detail: false);
        var kept = await _library.TakeAsync(track, 1);
        var lost = await _library.TakeAsync(track, 2);
        var orphan = await _library.TakeAsync(track, 3);
        var archivedFocusrite = await CalibrationAsync("Focusrite");
        var archivedOnly = await CalibrationAsync("Old interface");
        var mix = await MixWithTakeAsync(track, lost.Take.Id);
        await Factory.WithScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<StemStudioDbContext>();
            var take = await context.RecordingTakes.SingleAsync(item => item.Id == lost.Take.Id, Ct);
            take.RelinkCalibration(archivedFocusrite.Id);
            await context.Database.ExecuteSqlAsync($"UPDATE RecordingTakes SET MixId = {mix.Id.ToString().ToUpperInvariant()} WHERE Id = {lost.Take.Id.ToString().ToUpperInvariant()}", Ct);
            await context.SaveChangesAsync(Ct);
            return true;
        });
        await ScrubAsync([]);
        var liveFocusrite = await Factory.WithScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<StemStudioDbContext>();
            context.Assets.RemoveRange(await context.Assets.Where(asset => asset.Id == lost.Asset.Id || asset.Id == orphan.Asset.Id).ToListAsync(Ct));
            context.Mixes.Remove(await context.Mixes.SingleAsync(Ct));
            context.InputCalibrations.RemoveRange(await context.InputCalibrations.ToListAsync(Ct));
            var replacement = InputCalibration.Create(Guid.NewGuid(), new CalibrationValues("Focusrite", null, 900, CalibrationMethod.Manual, null, null, 0), LibraryFixture.T0);
            context.InputCalibrations.Add(replacement);
            await context.SaveChangesAsync(Ct);
            return replacement;
        });

        var plan = await PlanAsync(staged => staged.Assets.Where(asset => asset.Id == orphan.Asset.Id));

        var takes = plan.Added.OfType<RecordingTake>().ToList();
        var added = takes.ShouldHaveSingleItem();
        (added.Id, added.CalibrationId, added.MixId).ShouldBe((lost.Take.Id, liveFocusrite.Id, (Guid?)null));
        plan.Added.OfType<InputCalibration>().Select(calibration => calibration.Id).ShouldBe([archivedOnly.Id]);
        takes.ShouldNotContain(take => take.Id == kept.Take.Id);
    }

    private async Task<MergePlan> PlanAsync(Func<StemStudioDbContext, IQueryable<Asset>> removeFromStaged)
    {
        await using (var staged = StagedContext())
        {
            staged.Assets.RemoveRange(await removeFromStaged(staged).ToListAsync(Ct));
            await staged.SaveChangesAsync(Ct);
            await staged.Database.OpenConnectionAsync(Ct);
            await staged.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF; DELETE FROM Mixes;", Ct);
        }

        return await Factory.WithScopeAsync(async sp =>
        {
            await using var staged = StagedContext();
            return await new MergePlanner(staged, sp.GetRequiredService<StemStudioDbContext>()).PlanAsync(Ct);
        });
    }

    private async Task<Mix> MixWithTakeAsync(Track track, Guid takeId)
    {
        var mix = Mix.Create(Guid.NewGuid(), track, new MixSettings
        {
            Name = "With take " + Guid.NewGuid().ToString("N")[..4],
            Metronome = new MetronomeSettings { Enabled = false },
            Take = new TakeChannelSettings { TakeId = takeId, Volume = 0.7 },
        }, LibraryFixture.T0);
        await _library.AddAsync(mix);
        return mix;
    }

    private async Task<InputCalibration> CalibrationAsync(string label)
    {
        var calibration = InputCalibration.Create(Guid.NewGuid(), new CalibrationValues(label, null, 1102, CalibrationMethod.Loopback, 2, 8, 441), LibraryFixture.T0);
        await _library.AddAsync(calibration);
        return calibration;
    }

    private async Task ScrubAsync(IReadOnlyCollection<BackupCategory> excluded)
    {
        await new DatabaseSnapshot(TimeProvider.System, NullLogger.Instance).CreateAsync(LiveConnection, Staged, Ct);
        await new BackupScrubber().ScrubAsync(Staged, excluded, LibraryFixture.T0, Ct);
    }

    private StemStudioDbContext StagedContext() =>
        new(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite($"Data Source={Staged};Pooling=False").Options);
}
