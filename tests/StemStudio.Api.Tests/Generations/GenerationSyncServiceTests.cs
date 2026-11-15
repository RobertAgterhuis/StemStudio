using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Generations;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Generations;

/// <summary>009 T039: the generation sync service against the fake generator (FR-007, FR-008).</summary>
public sealed class GenerationSyncServiceTests : ApiTestBase
{
    private static readonly byte[] Take1 = [1, 2, 3, 4];
    private static readonly byte[] Take2 = [5, 6, 7];

    [Fact]
    public async Task Sync_MirrorsRunningReports()
    {
        var (generation, jobId) = await StartAsync(Factory, Client, takes: 2);
        Factory.Generator.SetJob(jobId, "RunningRestyle");

        await Factory.SyncGenerationsAsync();

        var stored = await FindAsync(generation);
        (stored.Status, stored.Progress, stored.Stage).ShouldBe((GenerationState.Running, 40, "Generating take 1 of 2"));
        stored.Device!.Name.ShouldBe("NVIDIA GeForce RTX 5070 Ti");
    }

    [Fact]
    public async Task Sync_MirrorsWaitingForTheGpu()
    {
        var (generation, jobId) = await StartAsync(Factory, Client);
        Factory.Generator.SetJob(jobId, "WaitingForGpuRestyle");

        await Factory.SyncGenerationsAsync();

        (await FindAsync(generation)).Stage.ShouldBe("Waiting for the GPU (needs 8.4 GB, 5.1 GB free)");
    }

    [Fact]
    public async Task Sync_CompletedDownloadsVerifiesAndRegistersEveryTake()
    {
        var (generation, jobId) = await StartAsync(Factory, Client, takes: 2);
        Factory.Generator.Complete(jobId, Take1, Take2);

        await Factory.SyncGenerationsAsync();

        var stored = await FindAsync(generation);
        stored.Status.ShouldBe(GenerationState.Completed);
        stored.Takes.Select(take => take.FilePath).ShouldBe([$"generations/{generation}/take-1.flac", $"generations/{generation}/take-2.flac"]);
        (await File.ReadAllBytesAsync(Path.Combine(Factory.DataDir, "generations", generation.ToString(), "take-1.flac"), Ct)).ShouldBe(Take1);
        (stored.Takes[0].DurationSeconds, stored.Takes[0].Seed, stored.Takes[0].Model.Id).ShouldBe((45.0, 1234567, "acestep-v15-turbo"));
        Factory.Generator.Deleted.ShouldBe([jobId]);
        var assets = await AssetsAsync();
        assets.Select(asset => asset.Id).ShouldBe(stored.LibraryAssetIds, ignoreOrder: true);
        var first = assets.Single(asset => asset.Provenance.Generation!.TakeIndex == 1);
        first.Name.ShouldBe("https://youtu.be/dQw4w9WgXcQ - Jazz trio (take 1)");
        (first.Kind, first.Status, first.Provenance.Origin, first.Provenance.TrackId).ShouldBe((AssetKind.Audio, AssetStatus.Ready, AssetOrigin.Generation, stored.SourceTrackId));
        (first.Audio!.SampleRate, first.Audio.BitDepth, first.Audio.PeakDbfs, first.Audio.DurationSeconds).ShouldBe((48_000, (int?)24, -0.9, 45.0));
        first.Files.Single().Sha256.ShouldBe(stored.Takes[0].Sha256);
    }

    [Fact]
    public async Task Sync_ADamagedTakeFailsTheGeneration()
    {
        var (generation, jobId) = await StartAsync(Factory, Client);
        Factory.Generator.Complete(jobId, Take1);
        Factory.Generator.SetJob(jobId, "CompletedRestyle", job => job["takes"]!.AsArray().RemoveAt(1));

        await Factory.SyncGenerationsAsync();

        var stored = await FindAsync(generation);
        (stored.Status, stored.Error!.Code).ShouldBe((GenerationState.Failed, GenerationErrorCode.EngineError));
        Directory.Exists(Path.Combine(Factory.DataDir, "generations", generation.ToString())).ShouldBeFalse();
        (await AssetsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_FailedJobsKeepTheGeneratorsError()
    {
        var (generation, jobId) = await StartAsync(Factory, Client);
        Factory.Generator.SetJob(jobId, "FailedOutOfMemory");

        await Factory.SyncGenerationsAsync();

        (await FindAsync(generation)).Error.ShouldBe(new GenerationError(GenerationErrorCode.OutOfMemory, "Not enough GPU memory for this request (try a shorter range or 1 take)."));
    }

    [Fact]
    public async Task Sync_RestartedGeneratorReportsInterrupted()
    {
        var (generation, jobId) = await StartAsync(Factory, Client);
        Factory.Generator.SetJob(jobId, "FailedOutOfMemory", job => job["error"] = new System.Text.Json.Nodes.JsonObject { ["code"] = "interrupted", ["message"] = "Interrupted by a restart." });

        await Factory.SyncGenerationsAsync();

        (await FindAsync(generation)).Error!.Code.ShouldBe(GenerationErrorCode.Interrupted);
    }

    [Fact]
    public async Task Sync_UnknownErrorCodesBecomeEngineErrors()
    {
        var (generation, jobId) = await StartAsync(Factory, Client);
        Factory.Generator.SetJob(jobId, "FailedOutOfMemory", job => job["error"]!["code"] = "somethingNew");

        await Factory.SyncGenerationsAsync();

        (await FindAsync(generation)).Error!.Code.ShouldBe(GenerationErrorCode.EngineError);
    }

    [Fact]
    public async Task Sync_CancelledByTheGenerator()
    {
        var (generation, jobId) = await StartAsync(Factory, Client);
        Factory.Generator.SetJob(jobId, "CancelledRestyle");

        await Factory.SyncGenerationsAsync();

        (await FindAsync(generation)).Status.ShouldBe(GenerationState.Cancelled);
    }

    [Fact]
    public async Task Sync_AJobTheGeneratorLostFailsAsInterrupted()
    {
        var (generation, jobId) = await StartAsync(Factory, Client);
        Factory.Generator.Forget(jobId);

        await Factory.SyncGenerationsAsync();

        var error = (await FindAsync(generation)).Error!;
        (error.Code, error.Message).ShouldBe((GenerationErrorCode.Interrupted, "The generator lost this generation (it was restarted)."));
    }

    [Fact]
    public async Task Sync_AnUnreachableGeneratorChangesNothing()
    {
        var (generation, _) = await StartAsync(Factory, Client);
        Factory.Generator.Override = _ => throw new HttpRequestException("down");

        await Factory.SyncGenerationsAsync();

        (await FindAsync(generation)).Status.ShouldBe(GenerationState.Queued);
    }

    [Fact]
    public async Task HostedService_PollsEveryTwoSeconds()
    {
        var (generation, jobId) = await StartAsync(Factory, Client);
        Factory.Generator.SetJob(jobId, "RunningRestyle");

        await Eventually.WaitUntilAsync(async () =>
        {
            Factory.Clock.Advance(TimeSpan.FromSeconds(2));
            return (await FindAsync(generation)).Status == GenerationState.Running;
        });
    }

    /// <summary>Creates a restyle through the API; returns its id and generator job id.</summary>
    internal static async Task<(Guid Generation, string JobId)> StartAsync(ApiFactory factory, HttpClient client, int takes = 1, string videoId = "dQw4w9WgXcQ")
    {
        var track = await RestyleEndpointTests.SeedTrackAsync(factory, videoId);
        using var response = await client.PostAsJsonAsync(new Uri(ApiRoutes.Restyles(track.Id), UriKind.Relative), RestyleEndpointTests.Request() with { Takes = takes }, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var generation = await response.BodyAsync<GenerationDto>();
        return (generation.Id, factory.Generator.Created[^1].Id);
    }

    /// <summary>Creates a restyle and completes it with one or two takes.</summary>
    internal static async Task<Guid> CompletedAsync(ApiFactory factory, HttpClient client, int takes = 2, string videoId = "dQw4w9WgXcQ")
    {
        var (generation, jobId) = await StartAsync(factory, client, takes, videoId);
        factory.Generator.Complete(jobId, takes == 1 ? [Take1] : [Take1, Take2]);
        await factory.SyncGenerationsAsync();
        return generation;
    }

    private Task<Generation> FindAsync(Guid id) =>
        Factory.WithScopeAsync(async sp => (await sp.GetRequiredService<IGenerationRepository>().FindAsync(id, CancellationToken.None))!);

    private Task<IReadOnlyList<Asset>> AssetsAsync() =>
        Factory.WithScopeAsync(async sp => (await sp.GetRequiredService<IAssetRepository>().ListAsync(new AssetQuery(), CancellationToken.None)).Items);
}
