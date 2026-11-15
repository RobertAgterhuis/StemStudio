using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Generations;

/// <summary>009 T041: list, get, cancel, retry, delete and take audio of generations.</summary>
public sealed class GenerationEndpointTests : ApiTestBase
{
    [Fact]
    public async Task List_NewestFirstWithFilters()
    {
        var (first, _) = await GenerationSyncServiceTests.StartAsync(Factory, Client);
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await StartOnNewTrackAsync();

        using var all = await GetAsync(ApiRoutes.Generations);
        using var restyles = await GetAsync($"{ApiRoutes.Generations}?kind=restyle&sourceTrackId={await SourceOfAsync(first)}");
        using var songs = await GetAsync($"{ApiRoutes.Generations}?kind=song");
        using var invalid = await GetAsync($"{ApiRoutes.Generations}?kind=nope");

        (await all.BodyAsync<List<GenerationDto>>()).Select(g => g.Id).ShouldBe([second, first]);
        (await restyles.BodyAsync<List<GenerationDto>>()).Select(g => g.Id).ShouldBe([first]);
        (await songs.BodyAsync<List<GenerationDto>>()).ShouldBeEmpty();
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        foreach (var response in new[] { all, restyles, songs, invalid })
        {
            await ContractAssert.MatchesAsync(response, "listGenerations");
        }
    }

    [Fact]
    public async Task Get_OneOr404()
    {
        var generation = await GenerationSyncServiceTests.CompletedAsync(Factory, Client);

        using var found = await GetAsync(ApiRoutes.Generation(generation));
        using var missing = await GetAsync(ApiRoutes.Generation(Guid.NewGuid()));

        var dto = await found.BodyAsync<GenerationDto>();
        dto.Takes.Select(take => take.AudioUrl).ShouldBe([ApiRoutes.TakeAudio(generation, 1), ApiRoutes.TakeAudio(generation, 2)]);
        dto.Takes.ShouldAllBe(take => take.AiGenerated && take.LibraryAssetId != null);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(found, "getGeneration");
        await ContractAssert.MatchesAsync(missing, "getGeneration");
    }

    [Fact]
    public async Task Cancel_ActiveAsksTheGeneratorTerminalIsUnchanged()
    {
        var (active, jobId) = await GenerationSyncServiceTests.StartAsync(Factory, Client);
        var completed = await GenerationSyncServiceTests.CompletedAsync(Factory, Client, videoId: "otherTrack1");

        using var cancelled = await PostAsync(ApiRoutes.CancelGeneration(active));
        using var unchanged = await PostAsync(ApiRoutes.CancelGeneration(completed));
        using var missing = await PostAsync(ApiRoutes.CancelGeneration(Guid.NewGuid()));

        (await cancelled.BodyAsync<GenerationDto>()).Status.ShouldBe(GenerationStateDto.Cancelled);
        (await unchanged.BodyAsync<GenerationDto>()).Status.ShouldBe(GenerationStateDto.Completed);
        Factory.Generator.Cancelled.ShouldBe([jobId]);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreach (var response in new[] { cancelled, unchanged, missing })
        {
            await ContractAssert.MatchesAsync(response, "cancelGeneration");
        }
    }

    [Fact]
    public async Task Cancel_WorksWhileTheGeneratorIsDown()
    {
        var (active, _) = await GenerationSyncServiceTests.StartAsync(Factory, Client);
        Factory.Generator.Override = _ => throw new HttpRequestException("down");

        using var response = await PostAsync(ApiRoutes.CancelGeneration(active));

        (await response.BodyAsync<GenerationDto>()).Status.ShouldBe(GenerationStateDto.Cancelled);
    }

    [Fact]
    public async Task Retry_CreatesANewGenerationWithNewSeeds()
    {
        var (failed, jobId) = await GenerationSyncServiceTests.StartAsync(Factory, Client, takes: 2);
        Factory.Generator.SetJob(jobId, "FailedOutOfMemory");
        await Factory.SyncGenerationsAsync();

        using var response = await PostAsync(ApiRoutes.RetryGeneration(failed));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var retry = await response.BodyAsync<GenerationDto>();
        retry.Id.ShouldNotBe(failed);
        response.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.Generation(retry.Id));
        retry.Status.ShouldBe(GenerationStateDto.Queued);
        retry.Restyle!.Caption.ShouldBe("Jazz swing, swing feel, piano, upright bass, brushed drums, relaxed, warm");
        Factory.Generator.Created[^1].Params["restyle"]!["seeds"]!.AsArray().Select(seed => seed!.GetValue<int>()).ShouldBe([3000, 4000]);
        await ContractAssert.MatchesAsync(response, "retryGeneration");
    }

    [Fact]
    public async Task Retry_OnlyFailedOrCancelledAndOnlyWhenReady()
    {
        var completed = await GenerationSyncServiceTests.CompletedAsync(Factory, Client);
        var (cancelled, _) = await GenerationSyncServiceTests.StartAsync(Factory, Client, videoId: "otherTrack1");
        (await PostAsync(ApiRoutes.CancelGeneration(cancelled))).Dispose();

        using var notRetryable = await PostAsync(ApiRoutes.RetryGeneration(completed));
        using var missing = await PostAsync(ApiRoutes.RetryGeneration(Guid.NewGuid()));
        Factory.Generator.HealthExample = "NoGpuHealth";
        Factory.Clock.Advance(TimeSpan.FromSeconds(11));
        using var notReady = await PostAsync(ApiRoutes.RetryGeneration(cancelled));

        await RestyleEndpointTests.AssertProblemAsync(notRetryable, HttpStatusCode.Conflict, "generation-not-retryable");
        await RestyleEndpointTests.AssertProblemAsync(notReady, HttpStatusCode.Conflict, "gpu-required");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreach (var response in new[] { notRetryable, missing, notReady })
        {
            await ContractAssert.MatchesAsync(response, "retryGeneration");
        }
    }

    [Fact]
    public async Task Retry_GeneratorDownIs503()
    {
        var (cancelled, _) = await GenerationSyncServiceTests.StartAsync(Factory, Client);
        (await PostAsync(ApiRoutes.CancelGeneration(cancelled))).Dispose();
        Factory.Generator.Override = request => request.Method == HttpMethod.Post ? new HttpResponseMessage(HttpStatusCode.BadGateway) : null;

        using var response = await PostAsync(ApiRoutes.RetryGeneration(cancelled));

        await RestyleEndpointTests.AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "generator-unavailable");
        await ContractAssert.MatchesAsync(response, "retryGeneration");
    }

    [Fact]
    public async Task Delete_RemovesFilesAndLibraryItemsButKeepsImportedTracks()
    {
        var generation = await GenerationSyncServiceTests.CompletedAsync(Factory, Client);
        var imported = await Factory.SeedAsync(SyncTestData.CompletedTrack("importedTak"));
        await Factory.WithScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IGenerationRepository>();
            (await repository.FindAsync(generation, CancellationToken.None))!.MarkImported(1, imported.Id, SyncTestData.T0);
            await repository.SaveAsync(CancellationToken.None);
            return true;
        });

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Generation(generation), UriKind.Relative), Ct);
        using var missing = await Client.DeleteAsync(new Uri(ApiRoutes.Generation(generation), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Directory.Exists(Path.Combine(Factory.DataDir, "generations", generation.ToString())).ShouldBeFalse();
        (await Factory.WithScopeAsync(async sp => (await sp.GetRequiredService<IAssetRepository>().ListAsync(new AssetQuery(), CancellationToken.None)).Total)).ShouldBe(0);
        Directory.EnumerateDirectories(Path.Combine(Factory.DataDir, "assets")).ShouldBeEmpty();
        (await Factory.FindTrackAsync(imported.Id)).ShouldNotBeNull();
        await ContractAssert.MatchesAsync(deleted, "deleteGeneration");
        await ContractAssert.MatchesAsync(missing, "deleteGeneration");
    }

    [Fact]
    public async Task Delete_AnActiveGenerationIsRemovedFromTheGenerator()
    {
        var (active, jobId) = await GenerationSyncServiceTests.StartAsync(Factory, Client);

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Generation(active), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Factory.Generator.Deleted.ShouldBe([jobId]);
    }

    [Fact]
    public async Task Delete_WorksWhileTheGeneratorIsDown()
    {
        var (active, _) = await GenerationSyncServiceTests.StartAsync(Factory, Client);
        Factory.Generator.Override = _ => throw new HttpRequestException("down");

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Generation(active), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TakeAudio_ServesFlacWithRanges()
    {
        var generation = await GenerationSyncServiceTests.CompletedAsync(Factory, Client);
        var path = ApiRoutes.TakeAudio(generation, 1);

        using var full = await GetAsync(path);
        using var partial = await SendAsync(path, request => request.Headers.Range = new RangeHeaderValue(1, 2));
        using var notModified = await SendAsync(path, request => request.Headers.IfNoneMatch.Add(full.Headers.ETag!));
        using var unsatisfiable = await SendAsync(path, request => request.Headers.Range = new RangeHeaderValue(50, 60));
        using var noTake = await GetAsync(ApiRoutes.TakeAudio(generation, 3));
        using var noGeneration = await GetAsync(ApiRoutes.TakeAudio(Guid.NewGuid(), 1));

        full.Content.Headers.ContentType!.MediaType.ShouldBe("audio/flac");
        (await full.Content.ReadAsByteArrayAsync(Ct)).ShouldBe([1, 2, 3, 4]);
        partial.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        (await partial.Content.ReadAsByteArrayAsync(Ct)).ShouldBe([2, 3]);
        (notModified.StatusCode, unsatisfiable.StatusCode, noTake.StatusCode, noGeneration.StatusCode)
            .ShouldBe((HttpStatusCode.NotModified, HttpStatusCode.RequestedRangeNotSatisfiable, HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        foreach (var response in new[] { full, partial, notModified, unsatisfiable, noTake, noGeneration })
        {
            await ContractAssert.MatchesAsync(response, "getTakeAudio");
        }
    }

    [Fact]
    public async Task TakeAudio_NotFoundBeforeCompletion()
    {
        var (active, _) = await GenerationSyncServiceTests.StartAsync(Factory, Client);

        using var response = await GetAsync(ApiRoutes.TakeAudio(active, 1));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletingTheSourceTrack_KeepsTheTitleSnapshot()
    {
        var generation = await GenerationSyncServiceTests.CompletedAsync(Factory, Client);
        var source = await SourceOfAsync(generation);

        (await Client.DeleteAsync(new Uri(ApiRoutes.Track(source), UriKind.Relative), Ct)).Dispose();

        using var response = await GetAsync(ApiRoutes.Generation(generation));
        var restyle = (await response.BodyAsync<GenerationDto>()).Restyle!;
        (restyle.SourceTrackId, restyle.SourceTrackTitle).ShouldBe((null, "https://youtu.be/dQw4w9WgXcQ"));
        await ContractAssert.MatchesAsync(response, "getGeneration");
    }

    private async Task<Guid> StartOnNewTrackAsync()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack("secondTrack"));
        var stems = Factory.Services.GetRequiredService<StemStudio.Api.Storage.IStemFileStore>();
        foreach (var stem in StemStudio.Domain.Tracks.StemCatalog.SixStem)
        {
            await stems.SaveStemAsync(track.Id, stem, (s, ct) => s.WriteAsync(new byte[] { 1 }, ct).AsTask(), CancellationToken.None);
        }

        using var response = await Client.PostAsync(
            new Uri(ApiRoutes.Restyles(track.Id), UriKind.Relative), System.Net.Http.Json.JsonContent.Create(RestyleEndpointTests.Request()), Ct);
        return (await response.BodyAsync<GenerationDto>()).Id;
    }

    private Task<Guid> SourceOfAsync(Guid generation) =>
        Factory.WithScopeAsync(async sp => (await sp.GetRequiredService<IGenerationRepository>().FindAsync(generation, CancellationToken.None))!.SourceTrackId!.Value);

    private Task<HttpResponseMessage> GetAsync(string path) => Client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    private Task<HttpResponseMessage> PostAsync(string path) => Client.PostAsync(new Uri(path, UriKind.Relative), null, Ct);

    private Task<HttpResponseMessage> SendAsync(string path, Action<HttpRequestMessage> configure)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        configure(request);
        return Client.SendAsync(request, Ct);
    }
}
