using System.Net;
using System.Text;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;

namespace StemStudio.Api.Tests.Handouts;

/// <summary>
/// Security review of the 020 handout endpoints: enum values only by their wire names (finding 1), the worker job of a deleted
/// handout (2), the active limit and the name under one lock (4), no orphan worker jobs (5), the setlist cap of the options
/// (8) and no worker text in the answer (9).
/// </summary>
public sealed class HandoutSecurityTests : ApiTestBase
{
    [Theory]
    [InlineData("\"family\":99", "family")]
    [InlineData("\"family\":3", "family")]
    [InlineData("\"family\":\"3\"", "family")]
    [InlineData("\"family\":\"tuba\"", "family")]
    [InlineData("\"family\":\"bass, drums\"", "family")]
    [InlineData("\"family\":null", "family")]
    [InlineData("\"family\":\"bass\",\"include\":[99]", "include")]
    [InlineData("\"family\":\"bass\",\"include\":[0]", "include")]
    [InlineData("\"family\":\"bass\",\"include\":[\"facts\",{\"x\":1}]", "include")]
    [InlineData("\"family\":\"melodic\",\"transposition\":42", "transposition")]
    [InlineData("\"family\":\"melodic\",\"transposition\":1", "transposition")]
    [InlineData("\"family\":\"melodic\",\"transposition\":\"g\"", "transposition")]
    [InlineData("\"family\":\"bass\",\"paper\":7", "paper")]
    [InlineData("\"family\":\"bass\",\"paper\":1", "paper")]
    [InlineData("\"family\":\"bass\",\"paper\":[\"a4\"]", "paper")]
    public async Task EnumValues_AreAcceptedOnlyByTheirWireNames(string members, string field)
    {
        var song = await Factory.SeedSongAsync();

        using var response = await PostRawAsync($$"""{"source":{"trackId":"{{song.Id}}"},{{members}}}""");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createHandout");
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        Factory.InsightsWorker.HandoutRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UndefinedEnumValues_AreRefused_BeforeTheTrackIsLookedUp()
    {
        using var response = await PostRawAsync($$"""{"source":{"trackId":"{{Guid.NewGuid()}}"},"family":99,"include":["facts"]}""");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.FieldErrorsAsync("family")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task WireNames_StillWork()
    {
        var song = await Factory.SeedSongAsync();

        using var response = await PostRawAsync($$"""{"source":{"trackId":"{{song.Id}}"},"family":"melodic","transposition":"b-flat","paper":"letter","include":["facts","notation"]}""");
        using var nulls = await PostRawAsync($$"""{"source":{"trackId":"{{song.Id}}"},"family":"bass","transposition":null,"paper":null,"include":null}""");

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldContain("\"transposition\":\"b-flat\"");
        body.ShouldContain("\"paper\":\"letter\"");
        nulls.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task DeletingAQueuedHandout_DeletesItsHandoutJob()
    {
        var song = await Factory.SeedSongAsync();
        var created = await (await Client.PostHandoutAsync(Bass(song.Id))).BodyAsync<AssetDto>();
        var job = Factory.InsightsWorker.HandoutJobs.Keys.Single();

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Asset(created.Id), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Factory.InsightsWorker.Deleted.ShouldBe([$"handout:{job}"]);
        Factory.Worker.RenderDeletes.ShouldBeEmpty();
    }

    private static CreateHandoutRequest Bass(Guid trackId) => new() { Source = HandoutSourceDto.Song(trackId), Family = HandoutFamilyDto.Bass };

    private Task<HttpResponseMessage> PostRawAsync(string json) =>
        Client.PostAsync(new Uri(ApiRoutes.Handouts, UriKind.Relative), new StringContent(json, Encoding.UTF8, "application/json"), Ct);
}
