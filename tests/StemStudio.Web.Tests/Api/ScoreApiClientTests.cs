using System.Net;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Sheet music operations of the typed client (backend contract 1.7.0, feature 008).</summary>
public sealed partial class StemStudioApiClientTests
{
    [Fact]
    public async Task GetScoreOptions_ForAChannel_EscapesTheChannel()
    {
        _handler.Respond(HttpStatusCode.OK, Json(ScoreData.Options("vocals.lead")));

        var options = await _client.GetScoreOptionsAsync(AudioSourceDto.FromChannel(TestData.TrackId, "vocals.lead"), Ct);

        options.SuggestedProfile.ShouldBe(ScoreProfileDto.Bass);
        _handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe($"/api/scores/options?trackId={TestData.TrackId}&channel=vocals.lead");
    }

    [Fact]
    public async Task GetScoreOptions_ForALibraryItem_UsesTheAssetQuery()
    {
        _handler.Respond(HttpStatusCode.OK, Json(ScoreData.Options()));

        await _client.GetScoreOptionsAsync(AudioSourceDto.FromAsset(AssetData.AssetId), Ct);

        _handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe($"/api/scores/options?assetId={AssetData.AssetId}");
    }

    [Fact]
    public async Task CreateScore_PostsTheRequest()
    {
        _handler.Respond(HttpStatusCode.Accepted, Json(ScoreData.Asset(AssetStatusDto.Queued)));
        var request = new CreateScoreRequest
        {
            Source = AudioSourceDto.FromChannel(TestData.TrackId, "bass"),
            Profile = ScoreProfileDto.Bass,
            Resolution = ScoreResolutionDto.Eighth,
            KeyOverride = "E minor",
        };

        var asset = await _client.CreateScoreAsync(request, Ct);

        asset.Kind.ShouldBe(AssetKindDto.Score);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Post, "/api/assets/scores"));
        sent.Body!.ShouldContain("\"profile\":\"bass\"");
        sent.Body!.ShouldContain("\"resolution\":\"eighth\"");
        sent.Body!.ShouldContain("\"keyOverride\":\"E minor\"");
        sent.Body!.ShouldContain("\"source\":{\"trackId\":");
    }
}
