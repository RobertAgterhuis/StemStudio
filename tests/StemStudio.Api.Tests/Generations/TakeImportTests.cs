using System.Net;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Tests.Generations;

/// <summary>009 T063: import a take as a track through the upload import (FR-009).</summary>
public sealed class TakeImportTests : ApiTestBase
{
    [Fact]
    public async Task Import_UploadsTheTakeAndMarksTheTrackGenerated()
    {
        var generation = await GenerationSyncServiceTests.CompletedAsync(Factory, Client);

        using var response = await ImportAsync(generation, 2);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var track = await response.BodyAsync<TrackDto>();
        response.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.Track(track.Id));
        track.Title.ShouldBe("https://youtu.be/dQw4w9WgXcQ - Jazz trio (take 2)");
        track.SourceKey.ShouldStartWith("upload:");
        track.Origin.ShouldBe(new TrackOriginDto
        {
            Kind = TrackOriginKindDto.Generated,
            GenerationId = generation,
            TakeIndex = 2,
            SourceTrackId = await SourceAsync(generation),
            AiGenerated = true,
        });
        Worker.Uploads.Single().Bytes.ShouldBe([5, 6, 7]);
        (await GenerationAsync(generation)).Takes[1].ImportedTrackId.ShouldBe(track.Id);
        await ContractAssert.MatchesAsync(response, "importTake");
    }

    [Fact]
    public async Task Import_IsIdempotent()
    {
        var generation = await GenerationSyncServiceTests.CompletedAsync(Factory, Client);
        var first = await (await ImportAsync(generation, 1)).BodyAsync<TrackDto>();

        using var again = await ImportAsync(generation, 1);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.BodyAsync<TrackDto>()).Id.ShouldBe(first.Id);
        Worker.Uploads.Count.ShouldBe(1);
        await ContractAssert.MatchesAsync(again, "importTake");
    }

    [Fact]
    public async Task Import_OriginShowsInTheTrackList()
    {
        var generation = await GenerationSyncServiceTests.CompletedAsync(Factory, Client);
        var imported = await (await ImportAsync(generation, 1)).BodyAsync<TrackDto>();

        using var list = await Client.GetAsync(new Uri(ApiRoutes.Tracks, UriKind.Relative), Ct);

        var tracks = await list.BodyAsync<List<TrackDto>>();
        tracks.Single(track => track.Id == imported.Id).Origin!.Kind.ShouldBe(TrackOriginKindDto.Generated);
        tracks.Single(track => track.Id != imported.Id).Origin.ShouldBeNull();
        (await list.Content.ReadAsStringAsync(Ct)).Split("\"origin\"").Length.ShouldBe(2);
    }

    [Fact]
    public async Task Import_NotCompletedIs409AndUnknownIs404()
    {
        var (active, _) = await GenerationSyncServiceTests.StartAsync(Factory, Client);
        var completed = await GenerationSyncServiceTests.CompletedAsync(Factory, Client, takes: 1, videoId: "otherTrack1");

        using var notCompleted = await ImportAsync(active, 1);
        using var noTake = await ImportAsync(completed, 2);
        using var noGeneration = await ImportAsync(Guid.NewGuid(), 1);

        await RestyleEndpointTests.AssertProblemAsync(notCompleted, HttpStatusCode.Conflict, "generation-not-completed");
        (noTake.StatusCode, noGeneration.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        foreach (var response in new[] { notCompleted, noTake, noGeneration })
        {
            await ContractAssert.MatchesAsync(response, "importTake");
        }
    }

    [Fact]
    public async Task Import_WorkerDownIs503()
    {
        var generation = await GenerationSyncServiceTests.CompletedAsync(Factory, Client);
        Worker.OnCreateUpload = _ => throw new WorkerUnavailableException();

        using var response = await ImportAsync(generation, 1);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await GenerationAsync(generation)).Takes[0].ImportedTrackId.ShouldBeNull();
        await ContractAssert.MatchesAsync(response, "importTake");
    }

    private Task<HttpResponseMessage> ImportAsync(Guid generation, int index) =>
        Client.PostAsync(new Uri(ApiRoutes.ImportTake(generation, index), UriKind.Relative), null, Ct);

    private async Task<GenerationDto> GenerationAsync(Guid id) =>
        await (await Client.GetAsync(new Uri(ApiRoutes.Generation(id), UriKind.Relative), Ct)).BodyAsync<GenerationDto>();

    private async Task<Guid?> SourceAsync(Guid id) => (await GenerationAsync(id)).Restyle!.SourceTrackId;
}
