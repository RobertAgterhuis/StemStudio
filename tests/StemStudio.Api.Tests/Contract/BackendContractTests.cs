using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using StemStudio.Api.Tests.Importing;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Importing;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Contract;

/// <summary>Responses of the running API checked against backend-api.openapi.yaml (1.5.0 since the 005/007 Phase 0; Constitution III, O2).</summary>
public sealed partial class BackendContractTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public BackendContractTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetHealth_MatchesContract()
    {
        using var response = await GetAsync(ApiRoutes.Health);

        await ContractAssert.MatchesAsync(response, "getHealth");
    }

    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT")]
    [InlineData("https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy")]
    public async Task ImportFromUrl_202And400MatchContract(string url)
    {
        using var response = await PostUrlAsync(url);

        await ContractAssert.MatchesAsync(response, "importFromUrl");
    }

    [Fact]
    public async Task ImportFromUrl_200MatchesContract()
    {
        (await PostUrlAsync("https://youtu.be/dQw4w9WgXcQ")).Dispose();

        using var response = await PostUrlAsync("https://youtu.be/dQw4w9WgXcQ");

        await ContractAssert.MatchesAsync(response, "importFromUrl");
    }

    [Fact]
    public async Task ImportFromUrl_MalformedJsonMatchesValidationProblem()
    {
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync(new Uri(ApiRoutes.Imports, UriKind.Relative), content, Ct);

        await ContractAssert.MatchesAsync(response, "importFromUrl");
    }

    [Fact]
    public async Task ImportFromUrl_503MatchesContract()
    {
        _factory.Worker.OnCreateUrl = _ => throw new WorkerUnavailableException();

        using var response = await PostUrlAsync("https://youtu.be/dQw4w9WgXcQ");

        await ContractAssert.MatchesAsync(response, "importFromUrl");
    }

    [Theory]
    [InlineData("song.mp3")]
    [InlineData("song.ogg")]
    [InlineData("empty.wav")]
    public async Task ImportFromUpload_202And415And400MatchContract(string fileName)
    {
        using var response = await PostUploadAsync(fileName.StartsWith("empty", StringComparison.Ordinal) ? [] : [1, 2, 3], fileName);

        await ContractAssert.MatchesAsync(response, "importFromUpload");
    }

    [Fact]
    public async Task ImportFromUpload_200And503MatchContract()
    {
        (await PostUploadAsync([1, 2, 3], "a.mp3")).Dispose();
        using var existing = await PostUploadAsync([1, 2, 3], "a.mp3");
        _factory.Worker.OnCreateUpload = _ => throw new WorkerUnavailableException();
        using var unavailable = await PostUploadAsync([4, 5, 6], "b.mp3");

        await ContractAssert.MatchesAsync(existing, "importFromUpload");
        await ContractAssert.MatchesAsync(unavailable, "importFromUpload");
    }

    [Fact]
    public async Task ImportFromUpload_413MatchesContract()
    {
        _factory.Worker.OnCreateUpload = _ => throw new WorkerRequestException(System.Net.HttpStatusCode.RequestEntityTooLarge, "too large");

        using var response = await PostUploadAsync([1], "a.mp3");

        await ContractAssert.MatchesAsync(response, "importFromUpload");
    }

    [Fact]
    public async Task ListTracks_WithEveryStateMatchesContract()
    {
        await SeedEveryStateAsync();

        using var response = await GetAsync(ApiRoutes.Tracks);

        await ContractAssert.MatchesAsync(response, "listTracks");
    }

    [Fact]
    public async Task GetTrack_200And404MatchContract()
    {
        var tracks = await SeedEveryStateAsync();

        foreach (var track in tracks)
        {
            using var response = await GetAsync(ApiRoutes.Track(track.Id));
            await ContractAssert.MatchesAsync(response, "getTrack");
        }

        using var missing = await GetAsync(ApiRoutes.Track(Guid.NewGuid()));
        await ContractAssert.MatchesAsync(missing, "getTrack");
    }

    [Fact]
    public async Task RetryTrack_EveryStatusMatchesContract()
    {
        var failed = await _factory.SeedAsync(SyncTestData.FailedTrack("aaaaaaaaaaa"));
        var completed = await _factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));
        var unavailable = await _factory.SeedAsync(SyncTestData.FailedTrack("ccccccccccc"));

        using var accepted = await PostAsync(ApiRoutes.RetryTrack(failed.Id));
        using var conflict = await PostAsync(ApiRoutes.RetryTrack(completed.Id));
        using var notFound = await PostAsync(ApiRoutes.RetryTrack(Guid.NewGuid()));
        _factory.Worker.OnCreateUrl = _ => throw new WorkerUnavailableException();
        using var serviceUnavailable = await PostAsync(ApiRoutes.RetryTrack(unavailable.Id));

        foreach (var response in new[] { accepted, conflict, notFound, serviceUnavailable })
        {
            await ContractAssert.MatchesAsync(response, "retryTrack");
        }
    }

    [Fact]
    public async Task DeleteTrack_EveryStatusMatchesContract()
    {
        var completed = await _factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa"));
        var queued = await _factory.SeedAsync(SyncTestData.UrlTrack("bbbbbbbbbbb"));

        using var noContent = await DeleteAsync(ApiRoutes.Track(completed.Id));
        using var conflict = await DeleteAsync(ApiRoutes.Track(queued.Id));
        using var notFound = await DeleteAsync(ApiRoutes.Track(Guid.NewGuid()));

        noContent.StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);
        conflict.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
        notFound.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        foreach (var response in new[] { noContent, conflict, notFound })
        {
            await ContractAssert.MatchesAsync(response, "deleteTrack");
        }
    }

    [Fact]
    public void Validator_RejectsNonConformingTracks()
    {
        var contract = OpenApiContract.Backend;
        var schema = contract.Root["components"]!["schemas"]!["Track"]!;
        var valid = contract.Root["components"]!["examples"]!["CompletedTrack"]!["value"]!;

        SchemaValidator.Validate(contract, schema, valid.DeepClone()).ShouldBeEmpty();
        ErrorsAfter(schema, valid, track => track["extra"] = 1).ShouldContain(e => e.Contains("additional property", StringComparison.Ordinal));
        ErrorsAfter(schema, valid, track => track.Remove("stems")).ShouldContain(e => e.Contains("required", StringComparison.Ordinal));
        ErrorsAfter(schema, valid, track => track["status"] = "done").ShouldContain(e => e.Contains("enum", StringComparison.Ordinal));
        ErrorsAfter(schema, valid, track => track["progress"] = 101).ShouldContain(e => e.Contains("maximum", StringComparison.Ordinal));
        ErrorsAfter(schema, valid, track => track["sourceKey"] = "vimeo:1").ShouldContain(e => e.Contains("does not match", StringComparison.Ordinal));
        ErrorsAfter(schema, valid, track => track["id"] = "nope").ShouldContain(e => e.Contains("uuid", StringComparison.Ordinal));
        ErrorsAfter(schema, valid, track => track["beatGrid"] = 3).ShouldContain(e => e.Contains("oneOf", StringComparison.Ordinal));
        ErrorsAfter(schema, valid, track => track["durationSeconds"] = 0).ShouldContain(e => e.Contains("exclusive", StringComparison.Ordinal));
        ErrorsAfter(schema, valid, track => track["title"] = "").ShouldContain(e => e.Contains("too short", StringComparison.Ordinal));
        ErrorsAfter(schema, valid, track => track["createdAt"] = "yesterday").ShouldContain(e => e.Contains("date-time", StringComparison.Ordinal));
    }

    [Fact]
    public void Contract_EveryOperationHasAnOperationId()
    {
        OpenApiContract.Backend.Operations.Select(op => op.Operation["operationId"]!.GetValue<string>())
            .ShouldBe(["getHealth", "getProcessingDevice", "importFromUrl", "importFromUpload", "listTracks", "getTrack", "deleteTrack", "retryTrack", "getStem", "startDetail", "cancelDetail", "getDetailStem", "listMixes", "createMix", "getMix", "updateMix", "deleteMix", "duplicateMix",
                "getTrackArtwork", "setTrackArtwork", "resetTrackArtwork", "getBandInfo", "lookupTrackMetadata",
                "listAssets", "createAudioExport", "createStemPack", "getAsset", "renameAsset", "deleteAsset", "retryAsset", "getAssetFile",
                "getLyrics", "setLyrics", "removeLyrics", "searchLyrics", "alignLyrics", "cancelLyricsAlignment", "setLyricsOffset",
                "getScoreOptions", "createScore", "createRestyle", "listStyles", "getStyle", "getGenerationStatus",
                "createStyle", "updateStyle", "deleteStyle", "duplicateStyle", "importTake", "createSong", "proposeSections",
                "listGenerations", "getGeneration", "deleteGeneration", "cancelGeneration", "retryGeneration", "getTakeAudio", .. PendingGenerationOperations, .. PendingSongVoiceOperations], ignoreOrder: true);
    }

    private static IReadOnlyList<string> ErrorsAfter(JsonNode schema, JsonNode valid, Action<JsonObject> change)
    {
        var copy = valid.DeepClone().AsObject();
        change(copy);
        return SchemaValidator.Validate(OpenApiContract.Backend, schema, copy);
    }

    private async Task<List<Track>> SeedEveryStateAsync()
    {
        var queued = SyncTestData.UrlTrack("aaaaaaaaaaa");
        var separating = SyncTestData.UrlTrack("bbbbbbbbbbb");
        separating.ApplyMetadata(new SourceMetadata("Some Song", "Some Artist"), SyncTestData.T0);
        separating.ApplyProgress(new TrackProgress(TrackStatus.Separating, "Separating stems", 47), SyncTestData.T0);
        var tracks = new List<Track> { queued, separating, SyncTestData.CompletedTrack("ccccccccccc"), SyncTestData.FailedTrack("ddddddddddd") };
        foreach (var track in tracks)
        {
            await _factory.SeedAsync(track);
        }

        return tracks;
    }

    private Task<HttpResponseMessage> GetAsync(string path) => _client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    private Task<HttpResponseMessage> DeleteAsync(string path) => _client.DeleteAsync(new Uri(path, UriKind.Relative), Ct);

    private Task<HttpResponseMessage> PostAsync(string path) => _client.PostAsync(new Uri(path, UriKind.Relative), null, Ct);

    private Task<HttpResponseMessage> PostUrlAsync(string url) =>
        _client.PostAsJsonAsync(new Uri(ApiRoutes.Imports, UriKind.Relative), new ImportUrlRequest(url), Ct);

    private async Task<HttpResponseMessage> PostUploadAsync(byte[] bytes, string fileName)
    {
        using var content = ImportEndpointsTests.UploadContent(bytes, fileName);
        return await _client.PostAsync(new Uri(ApiRoutes.ImportUpload, UriKind.Relative), content, Ct);
    }
}
