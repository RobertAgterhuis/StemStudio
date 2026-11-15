using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Detail;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks.Detail;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.Detail;

/// <summary>Detail separation of tracks imported before feature 002 and the worker cache (T072; FR-009, FR-010, SC-007).</summary>
public sealed class ExistingTrackDetailTests : IDisposable
{
    private static readonly Guid LegacyId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");
    private readonly string _dataDir = Directory.CreateTempSubdirectory("stemstudio-legacy-api-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_dataDir, true);
        }
        catch (IOException)
        {
            // A file may still be locked briefly on Windows; the OS temp cleanup removes it later.
        }
    }

    [Fact]
    public async Task TrackFromBeforeTheMigration_ExposesNoneAndCanBeSplitFurther()
    {
        await CreateLegacyDatabaseAsync();
        using var factory = new ApiFactory(_dataDir);
        using var client = factory.CreateClient();

        var before = await (await client.GetAsync(new Uri(ApiRoutes.Track(LegacyId), UriKind.Relative), Ct)).BodyAsync<TrackDto>();
        using var start = await client.PostAsync(new Uri(ApiRoutes.StartDetail(LegacyId), UriKind.Relative), null, Ct);
        await SyncAsync(factory);

        before.Detail.Status.ShouldBe(DetailPhaseStatusDto.None);
        start.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Worker.DetailCreates.ShouldBe(["3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c"]);
        (await factory.FindTrackAsync(LegacyId))!.DetailPhase!.Status.ShouldBe(DetailPhaseStatus.Queued);
    }

    [Fact]
    public async Task SecondTrackWithTheSameAudio_CompletesFromTheWorkerCacheInOnePass()
    {
        using var factory = new ApiFactory();
        factory.CreateClient().Dispose();
        var second = await factory.SeedAsync(SyncTestData.DetailQueued("bbbbbbbbbbb", detailJobId: null));
        factory.Worker.OnCreateDetail = _ => Task.FromResult(FakeWorkerClient.DetailJob(SyncTestData.DetailJobId, WorkerDetailJobState.Completed, 100));

        await SyncAsync(factory);

        var phase = (await factory.FindTrackAsync(second.Id))!.DetailPhase!;
        (phase.Status, phase.WorkerDetailJobId).ShouldBe((DetailPhaseStatus.Completed, SyncTestData.DetailJobId));
        factory.Services.GetRequiredService<IDetailStemFileStore>().Find(new DetailStemKey(second.Id, "drums", "kick")).ShouldNotBeNull();
        factory.Worker.DetailCreates.Count.ShouldBe(1);
    }

    private static Task<bool> SyncAsync(ApiFactory factory) => factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<DetailSynchronizer>().SyncAllAsync(Ct);
        return true;
    });

    private async Task CreateLegacyDatabaseAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={Path.Combine(_dataDir, "stemstudio.db")};Pooling=False");
        await connection.OpenAsync(Ct);
        await using (var context = new StemStudioDbContext(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(connection).Options))
        {
            await context.GetService<IMigrator>().MigrateAsync("20261114215330_Initial", Ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Tracks (Id, SourceType, SourceKey, SourceUrl, Title, Status, Stage, Progress, WorkerJobId, DurationSeconds, Model,
                CreatedAt, UpdatedAt, BeatGrid, Stems)
            VALUES ($id, 'Youtube', 'youtube:dQw4w9WgXcQ', 'https://youtu.be/dQw4w9WgXcQ', 'Song', 'Completed', 'Completed', 100,
                '3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c', 213.04, 'htdemucs', '2026-11-14 12:00:00', '2026-11-14 12:06:00', NULL,
                '[{"Name":"vocals","SizeBytes":1},{"Name":"drums","SizeBytes":1},{"Name":"bass","SizeBytes":1},{"Name":"other","SizeBytes":1}]');
            """;
        command.Parameters.AddWithValue("$id", LegacyId.ToString("D").ToUpperInvariant());
        await command.ExecuteNonQueryAsync(Ct);
    }
}
