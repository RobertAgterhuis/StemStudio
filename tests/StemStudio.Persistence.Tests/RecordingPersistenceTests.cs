using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Recording;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Feature 015 T024: takes, calibrations and the mix take channel in SQLite (data-model 2.1, 2.5, 2.6).</summary>
public sealed class RecordingPersistenceTests : IAsyncLifetime
{
    private static readonly DateTime T0 = TestData.T0;
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var context = NewContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private StemStudioDbContext NewContext() => new(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(_connection).Options);

    private async Task<string?> ScalarAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))?.ToString();
    }

    private static Asset TakeAsset(Guid trackId) => Asset.CreateRecording(Guid.NewGuid(), "Take 1", new Provenance
    {
        Origin = AssetOrigin.Recording,
        TrackId = trackId,
        TrackTitle = "Song",
        Contents = "Take 1 (recording)",
        Recording = new RecordingSource(Guid.NewGuid()),
    }, T0);

    private static RecordingTake Take(Guid trackId, Guid assetId, int number, Guid? mixId = null, Guid? calibrationId = null) => RecordingTake.Create(Guid.NewGuid(), new NewTake
    {
        TrackId = trackId,
        AssetId = assetId,
        Number = number,
        Part = RecordingPart.Piano,
        SessionId = Guid.NewGuid(),
        PassIndex = 2,
        Partial = false,
        StartSample = 1000,
        LengthSamples = 44_100,
        InputMode = InputChannelMode.Stereo,
        InputChannels = 2,
        LatencySamples = 1102,
        LatencyMethod = LatencyMethod.Manual,
        CalibrationId = calibrationId,
        MixId = mixId,
        Scan = new PeakScan(-12.5, 3, false),
        Dropouts = 0,
        RecordedAt = T0,
    }, T0.AddMinutes(number));

    private static async Task<(Track Track, Asset Asset)> SeedTrackAsync(StemStudioDbContext context)
    {
        var track = TestData.CompletedTrack(Guid.NewGuid().ToString("N")[..11]);
        var asset = TakeAsset(track.Id);
        context.Tracks.Add(track);
        context.Assets.Add(asset);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (track, asset);
    }

    [Fact]
    public async Task Take_RoundTrips_WithEnumsByName()
    {
        RecordingTake take;
        await using (var context = NewContext())
        {
            var (track, asset) = await SeedTrackAsync(context);
            take = Take(track.Id, asset.Id, 1);
            var repository = new RecordingRepository(context);
            repository.Add(take);
            await repository.SaveAsync(TestContext.Current.CancellationToken);
        }

        await using var read = NewContext();
        var loaded = (await new RecordingRepository(read).FindAsync(take.Id, TestContext.Current.CancellationToken))!;
        (loaded.Name, loaded.Part, loaded.InputMode, loaded.LatencyMethod, loaded.PeakDbfs, loaded.ClippedSamples, loaded.PassIndex).ShouldBe(("Take 1 - Keys", RecordingPart.Piano, InputChannelMode.Stereo, LatencyMethod.Manual, -12.5, 3, (int?)2));
        (await ScalarAsync($"SELECT Part || '/' || InputMode || '/' || LatencyMethod FROM RecordingTakes")).ShouldBe("Piano/Stereo/Manual");
        (await new RecordingRepository(read).FindByAssetAsync(take.AssetId, TestContext.Current.CancellationToken))!.Id.ShouldBe(take.Id);
    }

    [Fact]
    public async Task Number_IsUniquePerTrack()
    {
        await using var context = NewContext();
        var (track, asset) = await SeedTrackAsync(context);
        var other = TakeAsset(track.Id);
        context.Assets.Add(other);
        var repository = new RecordingRepository(context);
        repository.Add(Take(track.Id, asset.Id, 1));
        repository.Add(Take(track.Id, other.Id, 1));

        await Should.ThrowAsync<DuplicateKeyException>(() => repository.SaveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AssetId_IsUnique()
    {
        await using var context = NewContext();
        var (track, asset) = await SeedTrackAsync(context);
        context.RecordingTakes.Add(Take(track.Id, asset.Id, 1));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var second = NewContext();
        var repository = new RecordingRepository(second);
        repository.Add(Take(track.Id, asset.Id, 2));

        await Should.ThrowAsync<DuplicateKeyException>(() => repository.SaveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Indexes_Exist()
    {
        var indexes = await ScalarAsync("SELECT group_concat(name, ',') FROM sqlite_master WHERE type = 'index' AND tbl_name = 'RecordingTakes'");

        indexes.ShouldNotBeNull();
        indexes.ShouldContain("IX_RecordingTakes_TrackId_Number");
        indexes.ShouldContain("IX_RecordingTakes_TrackId_CreatedAt");
        indexes.ShouldContain("IX_RecordingTakes_TrackId_SessionId");
        indexes.ShouldContain("IX_RecordingTakes_AssetId");
        (await ScalarAsync("SELECT count(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_InputCalibrations_DeviceLabel'")).ShouldBe("1");
    }

    [Fact]
    public async Task DeletingTheTrack_CascadesToItsTakes()
    {
        await using var context = NewContext();
        var (track, asset) = await SeedTrackAsync(context);
        context.RecordingTakes.Add(Take(track.Id, asset.Id, 1));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlRawAsync("DELETE FROM Tracks", TestContext.Current.CancellationToken);

        (await ScalarAsync("SELECT count(*) FROM RecordingTakes")).ShouldBe("0");
    }

    [Fact]
    public async Task DeletingTheAsset_CascadesToItsTake()
    {
        await using var context = NewContext();
        var (track, asset) = await SeedTrackAsync(context);
        context.RecordingTakes.Add(Take(track.Id, asset.Id, 1));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlRawAsync("DELETE FROM Assets", TestContext.Current.CancellationToken);

        (await ScalarAsync("SELECT count(*) FROM RecordingTakes")).ShouldBe("0");
    }

    [Fact]
    public async Task DeletingTheMixOrCalibration_SetsNull_AndTheLatencyStays()
    {
        Guid takeId;
        InputCalibration calibration;
        await using (var context = NewContext())
        {
            var (track, asset) = await SeedTrackAsync(context);
            var mix = Mix.Create(Guid.NewGuid(), track, TestData.Settings(), T0);
            calibration = InputCalibration.Create(Guid.NewGuid(), new CalibrationValues("Focusrite", null, 1102, CalibrationMethod.Manual, null, null, 0), T0);
            context.Mixes.Add(mix);
            context.InputCalibrations.Add(calibration);
            var take = Take(track.Id, asset.Id, 1, mix.Id, calibration.Id);
            takeId = take.Id;
            context.RecordingTakes.Add(take);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM Mixes", TestContext.Current.CancellationToken);
        }

        await using (var context = NewContext())
        {
            IInputCalibrationRepository calibrations = new RecordingRepository(context);
            await calibrations.RemoveAsync((await calibrations.FindAsync(calibration.Id, TestContext.Current.CancellationToken))!, TestContext.Current.CancellationToken);
            await calibrations.SaveAsync(TestContext.Current.CancellationToken);
        }

        await using var read = NewContext();
        var loaded = (await read.RecordingTakes.SingleAsync(take => take.Id == takeId, TestContext.Current.CancellationToken))!;
        (loaded.MixId, loaded.CalibrationId, loaded.LatencySamples).ShouldBe(((Guid?)null, (Guid?)null, 1102));
    }

    [Fact]
    public async Task Calibrations_UniqueLabel_FindByLabel_AndListByLabel()
    {
        await using var context = NewContext();
        IInputCalibrationRepository calibrations = new RecordingRepository(context);
        calibrations.Add(InputCalibration.Create(Guid.NewGuid(), new CalibrationValues("b", null, 1, CalibrationMethod.Manual, null, null, 0), T0));
        calibrations.Add(InputCalibration.Create(Guid.NewGuid(), new CalibrationValues("a", "id", 2, CalibrationMethod.Loopback, 4, 8, 3), T0));
        await calibrations.SaveAsync(TestContext.Current.CancellationToken);

        (await calibrations.ListAsync(TestContext.Current.CancellationToken)).Select(c => c.DeviceLabel).ShouldBe(["a", "b"]);
        (await calibrations.FindByLabelAsync("a", TestContext.Current.CancellationToken))!.SpreadSamples.ShouldBe(4);
        (await calibrations.FindByLabelAsync("A", TestContext.Current.CancellationToken)).ShouldBeNull();
        calibrations.Add(InputCalibration.Create(Guid.NewGuid(), new CalibrationValues("a", null, 1, CalibrationMethod.Manual, null, null, 0), T0));
        await Should.ThrowAsync<DuplicateKeyException>(() => calibrations.SaveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MixTakeChannel_RoundTripsAsJson_AndNullWhenAbsent()
    {
        var takeId = Guid.NewGuid();
        Mix withTake;
        Mix without;
        await using (var context = NewContext())
        {
            var (track, _) = await SeedTrackAsync(context);
            withTake = Mix.Create(Guid.NewGuid(), track, TestData.Settings("A") with { Take = new TakeChannelSettings { TakeId = takeId, Volume = 0.5, Pan = -0.25, Mute = true, Solo = true } }, T0);
            without = Mix.Create(Guid.NewGuid(), track, TestData.Settings("B"), T0);
            context.Mixes.AddRange(withTake, without);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = NewContext();
        (await read.Mixes.SingleAsync(mix => mix.Id == withTake.Id, TestContext.Current.CancellationToken)).Take
            .ShouldBe(new TakeChannelSettings { TakeId = takeId, Volume = 0.5, Pan = -0.25, Mute = true, Solo = true });
        (await read.Mixes.SingleAsync(mix => mix.Id == without.Id, TestContext.Current.CancellationToken)).Take.ShouldBeNull();
        (await ScalarAsync($"SELECT count(*) FROM Mixes WHERE TakeChannel IS NULL")).ShouldBe("1");
    }

    [Fact]
    public async Task Queries_ListNewestFirst_Count_Ids_AndMixesShowingATake()
    {
        await using var context = NewContext();
        var (track, asset) = await SeedTrackAsync(context);
        var second = TakeAsset(track.Id);
        context.Assets.Add(second);
        var first = Take(track.Id, asset.Id, 1);
        var newer = Take(track.Id, second.Id, 2);
        var mix = Mix.Create(Guid.NewGuid(), track, TestData.Settings() with { Take = new TakeChannelSettings { TakeId = newer.Id } }, T0);
        context.Mixes.Add(mix);
        var repository = new RecordingRepository(context);
        repository.Add(first);
        repository.Add(newer);
        await repository.SaveAsync(TestContext.Current.CancellationToken);

        (await repository.ListByTrackAsync(track.Id, TestContext.Current.CancellationToken)).Select(take => take.Number).ShouldBe([2, 1]);
        (await repository.CountsAsync([track.Id, Guid.NewGuid()], TestContext.Current.CancellationToken)).ShouldBe(new Dictionary<Guid, int> { [track.Id] = 2 });
        (await repository.IdsOfTrackAsync(track.Id, TestContext.Current.CancellationToken)).ShouldBe(new HashSet<Guid> { first.Id, newer.Id }, ignoreOrder: true);
        (await repository.MixesShowingAsync(track.Id, [newer.Id], TestContext.Current.CancellationToken)).Single().Id.ShouldBe(mix.Id);
        (await repository.MixesShowingAsync(track.Id, [first.Id], TestContext.Current.CancellationToken)).ShouldBeEmpty();

        repository.Remove(first);
        await repository.SaveAsync(TestContext.Current.CancellationToken);
        (await repository.FindAsync(first.Id, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task TakeItems_AreNeverPendingFiling_AndHiddenFromTheListUnlessAskedFor()
    {
        Asset export;
        await using (var context = NewContext())
        {
            var (track, _) = await SeedTrackAsync(context);
            export = Asset.Create(Guid.NewGuid(), "Export", new Provenance
            {
                Origin = AssetOrigin.Export,
                TrackId = track.Id,
                TrackTitle = "Song",
                Contents = "drums, raw",
                Recipe = new Domain.Rendering.ExportRecipe { TrackId = track.Id, Mode = Domain.Rendering.ExportMode.Raw, Format = new Domain.Rendering.ExportFormat(Domain.Rendering.AudioFormat.Flac, 16, null) },
            }, T0);
            context.Assets.Add(export);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = NewContext();
        var library = new LibraryRepository(read);
        (await library.CountPendingAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        (await library.PendingAsync(10, TestContext.Current.CancellationToken)).Single().Id.ShouldBe(export.Id);
        var assets = new AssetRepository(read);
        var visible = await assets.ListAsync(new AssetQuery(), TestContext.Current.CancellationToken);
        (visible.Items.Single().Id, visible.Total).ShouldBe((export.Id, 1));
        var takes = await assets.ListAsync(new AssetQuery { Origin = AssetOrigin.Recording }, TestContext.Current.CancellationToken);
        (takes.Items.Single().Provenance.Origin, takes.Total).ShouldBe((AssetOrigin.Recording, 1));
        (await assets.ListAsync(new AssetQuery { Origin = AssetOrigin.Export, Status = AssetStatus.Queued }, TestContext.Current.CancellationToken)).Total.ShouldBe(1);
    }
}
