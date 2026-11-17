using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Library;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;
using static StemStudio.Api.Tests.Library.LibraryTestData;

namespace StemStudio.Api.Tests.Library;

/// <summary>022 T032: the start-up and on-demand filing of unfiled items (FR-010, SC-003, research R-11 refinements 1 and 2).</summary>
public sealed class LibraryFilingBackfillTests : IDisposable
{
    private readonly ApiFactory _factory = new() { Settings = new Dictionary<string, string?> { [LibraryFilingBackfill.BatchSizeKey] = "2" } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LibraryFilingBackfill Backfill => _factory.Services.GetRequiredService<LibraryFilingBackfill>();

    public void Dispose() => _factory.Dispose();

    private static Asset[] Unfiled(int count) =>
        [.. Enumerable.Range(0, count).Select(i => Asset.Create(Guid.NewGuid(), $"item {i}", ExportProvenance(Guid.NewGuid(), title: $"Song {i % 7}", artist: $"Band {i % 3}"), T0.AddSeconds(i)))];

    private Task<int> PendingAsync() => _factory.WithScopeAsync(sp => sp.GetRequiredService<ILibraryRepository>().CountPendingAsync(Ct));

    [Fact]
    public async Task Run_FilesEveryPendingItemInBatches()
    {
        await _factory.AddUnfiledAsync(Unfiled(5));

        (await Backfill.RunOnceAsync(Ct)).ShouldBe(5);

        (await PendingAsync()).ShouldBe(0);
        Backfill.Pending.ShouldBe(0);
        var all = await _factory.WithScopeAsync(sp => sp.GetRequiredService<ILibraryRepository>().AllItemsAsync(Ct));
        all.ShouldAllBe(asset => asset.IsFiled && asset.Placement!.Album.Name == LibraryKey.UnknownAlbum);
    }

    [Fact]
    public async Task Run_IsIdempotent()
    {
        await _factory.AddUnfiledAsync(Unfiled(3));
        await Backfill.RunOnceAsync(Ct);

        (await Backfill.RunOnceAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task StartUp_RunsAfterTheStartDelay()
    {
        await _factory.AddUnfiledAsync(Unfiled(3));
        await Task.Delay(100, Ct);
        (await PendingAsync()).ShouldBe(3);

        _factory.Clock.Advance(LibraryFilingBackfill.StartDelay);

        await WaitUntilAsync(async () => await PendingAsync() == 0, _factory);
    }

    [Fact]
    public async Task Trigger_StartsARunInTheBackground()
    {
        await _factory.AddUnfiledAsync(Unfiled(3));

        _factory.Services.GetRequiredService<ILibraryFilingTrigger>().Trigger();
        _factory.Services.GetRequiredService<ILibraryFilingTrigger>().Trigger();

        await WaitUntilAsync(async () => await PendingAsync() == 0, _factory);
    }

    [Fact]
    public async Task Maintenance_HoldsTheNextBatchAndRecountsAfterwards()
    {
        await _factory.AddUnfiledAsync(Unfiled(1));
        var gate = _factory.Services.GetRequiredService<LibraryGate>();
        gate.EnterMaintenance();

        var run = Backfill.RunOnceAsync(Ct);
        await Task.Delay(150, Ct);
        run.IsCompleted.ShouldBeFalse();
        (await PendingAsync()).ShouldBe(1);
        await _factory.AddUnfiledAsync(Unfiled(2));
        gate.ExitMaintenance();

        (await run).ShouldBe(3);
        Backfill.Pending.ShouldBe(0);
    }
    [Fact]
    public async Task Run_FilesAPre022UploadWithAHashedLookupTitleUnderItsOwnTitle()
    {
        const string sha = "28c19d35e0b1a4f6c7d8e9fa0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c";
        var track = Track.FromUpload(Guid.NewGuid(), SourceKey.ForUpload(sha), new UploadedFile("My demo.mp3", sha + ".mp3"), T0);
        track.ScheduleLookup(new LookupQuery(sha, null), T0);
        await _factory.SeedAsync(track);
        var item = Asset.Create(Guid.NewGuid(), "drums", ExportProvenance(track.Id, title: sha, artist: null), T0);
        await _factory.AddUnfiledAsync(item);

        (await Backfill.RunOnceAsync(Ct)).ShouldBe(1);

        var filed = await _factory.FindAssetAsync(item.Id);
        Level(filed!.Placement!.Song).ShouldBe(("My demo", PlacementSource.Import));
    }
}
