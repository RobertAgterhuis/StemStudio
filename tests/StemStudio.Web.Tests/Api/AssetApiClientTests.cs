using System.Net;
using System.Text.Json;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Library operations of the typed client (backend contract 1.5.0).</summary>
public sealed partial class StemStudioApiClientTests
{
    private static string Json(object value) => JsonSerializer.Serialize(value, StemStudioApiClient.Json);

    [Fact]
    public async Task ListAssets_GetsTheFirstFullPage()
    {
        _handler.Respond(HttpStatusCode.OK, Json(AssetData.List(AssetData.Asset())));

        var list = await _client.ListAssetsAsync(Ct);

        list.Items.ShouldHaveSingleItem().Name.ShouldBe("Never Gonna Give You Up - bass");
        list.TotalSizeBytes.ShouldBe(12_700_000);
        _handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/assets?limit=200");
    }

    [Fact]
    public async Task CreateAudioExport_PostsTheRequest()
    {
        _handler.Respond(HttpStatusCode.Accepted, Json(AssetData.Asset(status: AssetStatusDto.Queued)));
        var request = new CreateAudioExportRequest
        {
            TrackId = TestData.TrackId,
            Mode = ExportModeDto.Raw,
            Channels = ["bass"],
            Format = new ExportFormatDto(AudioFileFormatDto.Flac),
        };

        var asset = await _client.CreateAudioExportAsync(request, Ct);

        asset.Status.ShouldBe(AssetStatusDto.Queued);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Post, "/api/assets/audio-exports"));
        sent.Body!.ShouldContain("\"mode\":\"raw\"");
        sent.Body!.ShouldContain("\"channels\":[\"bass\"]");
    }

    [Fact]
    public async Task CreateStemPack_PostsTheRequest()
    {
        _handler.Respond(HttpStatusCode.Accepted, Json(new StemPackDto([AssetData.Asset()])));

        var pack = await _client.CreateStemPackAsync(new CreateStemPackRequest(TestData.TrackId, new ExportFormatDto(AudioFileFormatDto.Wav, 24), true), Ct);

        pack.Items.Count.ShouldBe(1);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        sent.PathAndQuery.ShouldBe("/api/assets/stem-packs");
        sent.Body!.ShouldContain("\"includeDetail\":true");
    }

    [Fact]
    public async Task ItemOperations_UseTheItemRoutes()
    {
        var id = AssetData.AssetId;
        _handler.Respond(HttpStatusCode.OK, Json(AssetData.Asset()))
            .Respond(HttpStatusCode.OK, Json(AssetData.Asset(name: "Drums")))
            .Respond(HttpStatusCode.Accepted, Json(AssetData.Asset(status: AssetStatusDto.Queued)))
            .Respond(HttpStatusCode.NoContent);

        await _client.GetAssetAsync(id, Ct);
        (await _client.RenameAssetAsync(id, "Drums", Ct)).Name.ShouldBe("Drums");
        await _client.RetryAssetAsync(id, Ct);
        await _client.DeleteAssetAsync(id, Ct);

        _handler.Requests.Select(r => (r.Method.Method, r.PathAndQuery)).ShouldBe(
        [
            ("GET", $"/api/assets/{id}"), ("PATCH", $"/api/assets/{id}"), ("POST", $"/api/assets/{id}/retry"), ("DELETE", $"/api/assets/{id}"),
        ]);
        _handler.Requests[1].Body.ShouldBe("""{"name":"Drums"}""");
    }

    [Fact]
    public async Task SourceUnavailable_IsAReadableConflict()
    {
        _handler.RespondProblem(HttpStatusCode.Conflict, """{"type":"source-unavailable","title":"Source unavailable","status":409,"detail":"The stem file of bass is missing."}""");

        var error = (await Should.ThrowAsync<ApiException>(() => _client.RetryAssetAsync(AssetData.AssetId, Ct))).Error;

        error.UserMessage.ShouldBe("The stem file of bass is missing.");
    }
}
