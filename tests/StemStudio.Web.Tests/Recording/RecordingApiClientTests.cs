using System.Net;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;
using static StemStudio.Web.Tests.Recording.RecordingData;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T042/T043: take and calibration operations of the typed client (backend 1.14.0, tag recording).</summary>
public sealed class RecordingApiClientTests : IDisposable
{
    private readonly StubHttpMessageHandler _handler = new();
    private readonly StemStudioApiClient _client;

    public RecordingApiClientTests() => _client = new StemStudioApiClient(StubHttpMessageHandler.CreateClient(_handler));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _handler.Dispose();

    [Fact]
    public async Task CreateTake_StreamsAudioAndJsonMetadataParts()
    {
        _handler.Respond(HttpStatusCode.Accepted, Body(Take(status: TakeStatusDto.Saving)));
        await using var wav = new MemoryStream(new byte[4096]);

        var take = await _client.CreateTakeAsync(TrackId, wav, Upload(), Ct);

        take.Status.ShouldBe(TakeStatusDto.Saving);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Post, $"/api/tracks/{TrackId:D}/takes"));
        sent.Parts.Select(p => (p.Name, p.FileName, p.MediaType?.MediaType)).ShouldBe(
            [("audio", "capture.wav", "audio/wav"), ("metadata", null, "application/json")]);
        sent.Parts[0].ContentType.ShouldBe(typeof(StreamContent));
        sent.Parts[0].Length.ShouldBe(4096);
    }

    [Fact]
    public async Task ReplaceTakeAudio_PutsTheSameParts()
    {
        _handler.Respond(HttpStatusCode.Accepted, Body(Take(status: TakeStatusDto.Saving)));
        await using var wav = new MemoryStream(new byte[64]);

        await _client.ReplaceTakeAudioAsync(TakeId, wav, new ReplaceTakeAudioMetadataDto { StartSample = 1, LengthSamples = 2, AdjustmentSamples = -441 }, Ct);

        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Put, $"/api/takes/{TakeId:D}/audio"));
        sent.Parts.Select(p => p.Name).ShouldBe(["audio", "metadata"]);
    }

    [Fact]
    public async Task TakeReads_UseTheirRoutes()
    {
        _handler.Respond(HttpStatusCode.OK, Body(List(Take()))).Respond(HttpStatusCode.OK, Body(Take()))
            .Respond(HttpStatusCode.Accepted, Body(Take(status: TakeStatusDto.Saving)));

        (await _client.ListTakesAsync(TrackId, Ct)).TotalSizeBytes.ShouldBe(1_200_000);
        (await _client.GetTakeAsync(TakeId, Ct)).Name.ShouldBe("Take 1");
        (await _client.RetryTakeAsync(TakeId, Ct)).Status.ShouldBe(TakeStatusDto.Saving);

        _handler.Requests.Select(r => (r.Method.Method, r.PathAndQuery)).ShouldBe(
        [
            ("GET", $"/api/tracks/{TrackId:D}/takes"), ("GET", $"/api/takes/{TakeId:D}"), ("POST", $"/api/takes/{TakeId:D}/retry"),
        ]);
    }

    [Fact]
    public async Task UpdateTake_SendsOnlyTheChangedFields()
    {
        _handler.Respond(HttpStatusCode.OK, Body(Take()));

        await _client.UpdateTakeAsync(TakeId, new UpdateTakeRequest { Favorite = true }, Ct);

        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.Body).ShouldBe((HttpMethod.Patch, """{"favorite":true}"""));
    }

    [Fact]
    public async Task Deletes_UseTheirRoutes_AndReturnTheCount()
    {
        _handler.Respond(HttpStatusCode.NoContent).Respond(HttpStatusCode.OK, """{"deleted":2}""").Respond(HttpStatusCode.NoContent);
        var other = Guid.NewGuid();

        await _client.DeleteTakeAsync(TakeId, Ct);
        var deleted = await _client.DeleteTakesAsync(TrackId, [TakeId, other], Ct);
        await _client.DeleteCalibrationAsync(CalibrationId, Ct);

        deleted.ShouldBe(2);
        _handler.Requests[1].Body.ShouldBe($$"""{"takeIds":["{{TakeId:D}}","{{other:D}}"]}""");
        _handler.Requests.Select(r => (r.Method.Method, r.PathAndQuery)).ShouldBe(
        [
            ("DELETE", $"/api/takes/{TakeId:D}"), ("POST", $"/api/tracks/{TrackId:D}/takes/delete"),
            ("DELETE", $"/api/recording/calibrations/{CalibrationId:D}"),
        ]);
    }

    [Fact]
    public async Task Calibrations_ListAndUpsert()
    {
        _handler.Respond(HttpStatusCode.OK, Body(new[] { Calibration() })).Respond(HttpStatusCode.OK, Body(Calibration()));
        var input = new InputCalibrationInput
        {
            DeviceLabel = "USB Mic",
            LatencySamples = 1102,
            Method = CalibrationMethodDto.Manual,
            OutputLatencySamples = 300,
        };

        (await _client.ListCalibrationsAsync(Ct)).ShouldHaveSingleItem().LatencySamples.ShouldBe(1102);
        await _client.UpsertCalibrationAsync(input, Ct);

        var put = _handler.Requests[1];
        (put.Method, put.PathAndQuery).ShouldBe((HttpMethod.Put, "/api/recording/calibrations"));
        put.Body!.ShouldContain("\"method\":\"manual\"");
    }

    [Fact]
    public async Task RefusedUpload_ThrowsTheProblem()
    {
        _handler.RespondProblem(HttpStatusCode.UnprocessableEntity,
            """{"type":"silent-take","title":"The take is silent","status":422,"detail":"Nothing was recorded."}""");
        await using var wav = new MemoryStream(new byte[8]);

        var error = await Should.ThrowAsync<ApiException>(() => _client.CreateTakeAsync(TrackId, wav, Upload(), Ct));

        (error.Error.Status, error.Error.Code).ShouldBe((HttpStatusCode.UnprocessableEntity, "silent-take"));
    }
}
