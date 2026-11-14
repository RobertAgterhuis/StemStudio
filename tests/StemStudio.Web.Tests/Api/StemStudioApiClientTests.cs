using System.Net;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

public sealed class StemStudioApiClientTests : IDisposable
{
    private readonly StubHttpMessageHandler _handler = new();
    private readonly StemStudioApiClient _client;

    public StemStudioApiClientTests() => _client = new StemStudioApiClient(StubHttpMessageHandler.CreateClient(_handler));

    public void Dispose() => _handler.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ImportUrl_PostsCamelCaseJson_AndReturnsTrack()
    {
        _handler.Respond(HttpStatusCode.Accepted, TestData.CompletedTrackJson);

        var track = await _client.ImportUrlAsync("https://youtu.be/dQw4w9WgXcQ", Ct);

        track.Status.ShouldBe(TrackStatusDto.Completed);
        track.BeatGrid!.Bpm.ShouldBe(113.5);
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.PathAndQuery.ShouldBe("/api/imports");
        request.Body.ShouldBe("""{"url":"https://youtu.be/dQw4w9WgXcQ"}""");
    }

    [Fact]
    public async Task ImportUpload_SendsMultipartWithStreamContent()
    {
        _handler.Respond(HttpStatusCode.Accepted, TestData.CompletedTrackJson);
        await using var stream = new MemoryStream(new byte[600_000]);

        await _client.ImportUploadAsync(new UploadFile(stream, "song.mp3", "audio/mpeg"), Ct);

        var request = _handler.Requests.ShouldHaveSingleItem();
        request.PathAndQuery.ShouldBe("/api/imports/upload");
        request.ContentType.ShouldBe("multipart/form-data");
        var part = request.Parts.ShouldHaveSingleItem();
        part.ContentType.ShouldBe(typeof(StreamContent));
        part.Name.ShouldBe("file");
        part.FileName.ShouldBe("song.mp3");
        part.Length.ShouldBe(600_000);
    }

    [Fact]
    public async Task TrackOperations_UseContractRoutes()
    {
        _handler.Respond(HttpStatusCode.OK, $"[{TestData.CompletedTrackJson}]")
            .Respond(HttpStatusCode.OK, TestData.CompletedTrackJson)
            .Respond(HttpStatusCode.Accepted, TestData.CompletedTrackJson);

        (await _client.ListTracksAsync(Ct)).ShouldHaveSingleItem().Id.ShouldBe(TestData.TrackId);
        (await _client.GetTrackAsync(TestData.TrackId, Ct)).Title.ShouldBe("Never Gonna Give You Up");
        await _client.RetryTrackAsync(TestData.TrackId, Ct);

        _handler.Requests.Select(r => $"{r.Method} {r.PathAndQuery}").ShouldBe(
        [
            "GET /api/tracks",
            $"GET /api/tracks/{TestData.TrackId}",
            $"POST /api/tracks/{TestData.TrackId}/retry",
        ]);
    }

    [Fact]
    public async Task MixOperations_UseContractRoutes()
    {
        var summary = """[{"id":"6a1f0c9e-2b3d-4e5f-8a9b-0c1d2e3f4a5b","trackId":"0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a","trackTitle":"T","name":"N","updatedAt":"2026-11-14T13:00:00Z"}]""";
        _handler.Respond(HttpStatusCode.OK, summary).Respond(HttpStatusCode.OK, summary)
            .Respond(HttpStatusCode.OK, TestData.MixJson).Respond(HttpStatusCode.Created, TestData.MixJson)
            .Respond(HttpStatusCode.OK, TestData.MixJson).Respond(HttpStatusCode.NoContent)
            .Respond(HttpStatusCode.Created, TestData.MixJson);

        (await _client.ListMixesAsync(null, Ct)).ShouldHaveSingleItem().TrackTitle.ShouldBe("T");
        await _client.ListMixesAsync(TestData.TrackId, Ct);
        (await _client.GetMixAsync(TestData.MixId, Ct)).Settings.Loop.ShouldBe(new LoopRegionDto(10, 20, true));
        await _client.CreateMixAsync(new CreateMixRequest(TestData.TrackId, TestData.Settings()), Ct);
        await _client.UpdateMixAsync(TestData.MixId, TestData.Settings(), Ct);
        await _client.DeleteMixAsync(TestData.MixId, Ct);
        await _client.DuplicateMixAsync(TestData.MixId, Ct);

        _handler.Requests.Select(r => $"{r.Method} {r.PathAndQuery}").ShouldBe(
        [
            "GET /api/mixes",
            $"GET /api/mixes?trackId={TestData.TrackId}",
            $"GET /api/mixes/{TestData.MixId}",
            "POST /api/mixes",
            $"PUT /api/mixes/{TestData.MixId}",
            $"DELETE /api/mixes/{TestData.MixId}",
            $"POST /api/mixes/{TestData.MixId}/duplicate",
        ]);
        _handler.Requests[3].Body!.ShouldContain("\"trackId\":\"0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a\"");
        _handler.Requests[4].Body!.ShouldContain("\"bpmOverride\":120");
    }

    [Fact]
    public async Task ValidationProblem_MapsFieldErrors()
    {
        _handler.RespondProblem(HttpStatusCode.BadRequest, """
            {"title":"One or more validation errors occurred.","status":400,
             "errors":{"url":["Only YouTube video links and Spotify track links are supported."]}}
            """);

        var error = (await Should.ThrowAsync<ApiException>(() => _client.ImportUrlAsync("x", Ct))).Error;

        error.Status.ShouldBe(HttpStatusCode.BadRequest);
        error.FieldErrors["url"].ShouldBe(["Only YouTube video links and Spotify track links are supported."]);
        error.UserMessage.ShouldBe("Only YouTube video links and Spotify track links are supported.");
    }

    [Fact]
    public async Task Conflict_And_Unavailable_AreDistinct()
    {
        _handler.RespondProblem(HttpStatusCode.Conflict, """{"title":"Mix name taken","status":409,"detail":"A mix named \"X\" already exists for this track."}""")
            .RespondProblem(HttpStatusCode.ServiceUnavailable, """{"title":"Processing service unavailable","status":503,"detail":"The processing service is unavailable."}""");

        var conflict = (await Should.ThrowAsync<ApiException>(() => _client.CreateMixAsync(new CreateMixRequest(TestData.TrackId, TestData.Settings()), Ct))).Error;
        var unavailable = (await Should.ThrowAsync<ApiException>(() => _client.ImportUrlAsync("x", Ct))).Error;

        conflict.IsConflict.ShouldBeTrue();
        conflict.IsUnavailable.ShouldBeFalse();
        conflict.UserMessage.ShouldBe("A mix named \"X\" already exists for this track.");
        unavailable.IsUnavailable.ShouldBeTrue();
        unavailable.IsConflict.ShouldBeFalse();
        unavailable.Title.ShouldBe("Processing service unavailable");
    }

    [Fact]
    public async Task ProblemWithoutDetail_UsesTitle()
    {
        _handler.RespondProblem(HttpStatusCode.NotFound, """{"title":"Not found","status":404}""");

        var error = (await Should.ThrowAsync<ApiException>(() => _client.GetMixAsync(TestData.MixId, Ct))).Error;

        error.UserMessage.ShouldBe("Not found");
        error.IsNotFound.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "text/plain")]
    [InlineData("not json", "application/json")]
    [InlineData("<html/>", "text/html")]
    public async Task NonProblemBody_FallsBackToStatusText(string? body, string mediaType)
    {
        _handler.Respond(HttpStatusCode.InternalServerError, body, mediaType);

        var error = (await Should.ThrowAsync<ApiException>(() => _client.ListTracksAsync(Ct))).Error;

        error.Status.ShouldBe(HttpStatusCode.InternalServerError);
        error.UserMessage.ShouldBe("The server returned an error (500).");
    }

    [Fact]
    public async Task NetworkFailure_IsReportedAsApiUnreachable()
    {
        _handler.ThrowOnSend = new HttpRequestException("connection refused");

        var exception = await Should.ThrowAsync<ApiException>(() => _client.ListTracksAsync(Ct));

        exception.Error.IsUnavailable.ShouldBeTrue();
        exception.Message.ShouldBe("The StemStudio API cannot be reached. Is it running?");
        exception.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task EmptySuccessBody_Throws()
    {
        _handler.Respond(HttpStatusCode.OK, "null");

        await Should.ThrowAsync<ApiException>(() => _client.GetTrackAsync(TestData.TrackId, Ct));
    }

    [Fact]
    public void ApiException_StandardConstructors()
    {
        new ApiException().Error.Status.ShouldBe(HttpStatusCode.InternalServerError);
        new ApiException("boom").Message.ShouldBe("boom");
        new ApiException("boom", new InvalidOperationException()).InnerException.ShouldNotBeNull();
    }
}
