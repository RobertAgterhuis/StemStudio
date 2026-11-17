using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;

namespace StemStudio.Api.Tests.Worker;

/// <summary>013 T023: storage report, clean-up and rehydration of the worker client against the worker 1.7.0 examples.</summary>
public sealed class WorkerClientStorageTests
{
    private const string TrackId = "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f";
    private const string Sha = "6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static WorkerRehydrateRequest Rehydrate => new(Guid.Parse(TrackId), "youtube", "youtube:dQw4w9WgXcQ", "htdemucs_6s", ["vocals", "drums", "bass", "guitar", "piano", "other"]);

    [Fact]
    public async Task GetStorage_ParsesTheReport()
    {
        var (client, handler) = WorkerClientTests.Create(_ => Example(HttpStatusCode.OK, "WorkerStorageReport"));

        var storage = await client.GetStorageAsync(Ct);

        handler.Requests.Single().Uri.AbsolutePath.ShouldBe("/v1/storage");
        storage.MeasuredAt.ShouldBe(new DateTimeOffset(2026, 11, 16, 14, 0, 0, TimeSpan.Zero));
        storage.Volumes.Select(volume => volume.Id).ShouldBe(["data", "models"]);
        storage.Volumes[0].FreeBytes.ShouldBe(805306368000);
        var entry = storage.CacheEntries.ShouldHaveSingleItem();
        (entry.AudioSha256, entry.StandardBytes, entry.DetailBytes, entry.PartialBytes, entry.ArtworkBytes, entry.InUse, entry.FromLibrary).ShouldBe(
            (Sha, 151000000L, 402000000L, 0L, 120000L, false, false));
        entry.JobIds.ShouldBe(["3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c"]);
        entry.LastUsedAt.ShouldBe(new DateTimeOffset(2026, 11, 15, 20, 0, 0, TimeSpan.Zero));
        (storage.JobFilesBytes, storage.JobFilesCount, storage.ScratchBytes).ShouldBe((5242880L, 412, 0L));
        storage.ModelCaches.Select(cache => (cache.Id, cache.Bytes)).ShouldBe([("torch", 335544320L), ("whisper", 1717986918L)]);
        storage.ModelCaches[0].Models.ShouldBe(["htdemucs_6s", "htdemucs"]);
    }

    [Fact]
    public async Task GetStorage_AnUnresponsiveWorkerIsUnavailableAfterTwoSeconds()
    {
        var clock = new FakeTimeProvider();
        var http = new HttpClient(new NeverHandler()) { BaseAddress = new Uri("http://worker:8000") };
        var client = new WorkerClient(http, NullLogger<WorkerClient>.Instance, clock);

        var call = client.GetStorageAsync(Ct);
        clock.Advance(TimeSpan.FromMilliseconds(1999));
        call.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(1));

        await Should.ThrowAsync<WorkerUnavailableException>(() => call);
        WorkerTimeouts.Storage.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CleanStorage_PostsTheRequestAndParsesTheSkippedInUseEntry()
    {
        var (client, handler) = WorkerClientTests.Create(_ => Example(HttpStatusCode.OK, "StorageCleanupDone"));

        var result = await client.CleanStorageAsync(new WorkerStorageCleanupRequest([Sha], Scratch: true, OldJobFiles: false), Ct);

        (result.FreedBytes, result.RemovedEntries.Single()).ShouldBe((553120000L, Sha));
        var skipped = result.Skipped.ShouldHaveSingleItem();
        (skipped.AudioSha256, skipped.Reason).ShouldBe(("d4735e3a265e16eee03f59718b9b5d03019c07d8b6c51f90da3a666eec13ab35", "inUse"));
        var request = handler.Requests.Single();
        (request.Method, request.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/storage/cleanup"));
        using var body = JsonDocument.Parse(request.BodyText);
        body.RootElement.GetProperty("cacheEntries")[0].GetString().ShouldBe(Sha);
        (body.RootElement.GetProperty("scratch").GetBoolean(), body.RootElement.GetProperty("oldJobFiles").GetBoolean()).ShouldBe((true, false));
    }

    [Theory]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.OK)]
    public async Task RehydrateJob_PostsTheTrackAndParsesTheJob(HttpStatusCode status)
    {
        var (client, handler) = WorkerClientTests.Create(_ => Example(status, "RehydratedJobCreated"));

        var job = await client.RehydrateJobAsync(Rehydrate, Ct);

        (job.JobId, job.Status, job.Model).ShouldBe(("5d4c3b2a1f0e4d9c8b7a6f5e4d3c2b1a", "completed", "htdemucs_6s"));
        job.AudioSha256.ShouldBe("4e07408562bedb8b60ce05c1decfe3ad16b72230967de01f640b7e4729b49fce");
        job.Stems.Count.ShouldBe(6);
        var request = handler.Requests.Single();
        (request.Method, request.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/jobs/rehydrate"));
        using var body = JsonDocument.Parse(request.BodyText);
        body.RootElement.GetProperty("trackId").GetString().ShouldBe(TrackId);
        body.RootElement.GetProperty("sourceType").GetString().ShouldBe("youtube");
        body.RootElement.GetProperty("sourceKey").GetString().ShouldBe("youtube:dQw4w9WgXcQ");
        body.RootElement.GetProperty("stems").GetArrayLength().ShouldBe(6);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "piano")]
    [InlineData(HttpStatusCode.Conflict, null)]
    [InlineData(HttpStatusCode.UnprocessableEntity, null)]
    public async Task RehydrateJob_RejectionsBecomeRequestExceptions(HttpStatusCode status, string? stem)
    {
        var problem = stem is null
            ? """{"type":"https://stemstudio.local/problems/library-unavailable","title":"Library unavailable","status":409,"detail":"The library mount is not readable."}"""
            : """{"type":"https://stemstudio.local/problems/stem-missing","title":"Stem missing in the library","status":404,"detail":"tracks/x/stems/piano.flac does not exist.","stem":"piano"}""";
        var (client, _) = WorkerClientTests.Create(_ => StubHttpMessageHandler.Json(status, problem, "application/problem+json"));

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.RehydrateJobAsync(Rehydrate, Ct));

        error.StatusCode.ShouldBe(status);
        error.Detail.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task StorageCalls_5xxAreUnavailable()
    {
        var (client, _) = WorkerClientTests.Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetStorageAsync(Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.CleanStorageAsync(new WorkerStorageCleanupRequest([], false, true), Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.RehydrateJobAsync(Rehydrate, Ct));
    }

    private static HttpResponseMessage Example(HttpStatusCode status, string name)
    {
        var contract = OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "worker-api.openapi.yaml"));
        return StubHttpMessageHandler.Json(status, contract.Root["components"]!["examples"]![name]!["value"]!.ToJsonString());
    }

    private sealed class NeverHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
