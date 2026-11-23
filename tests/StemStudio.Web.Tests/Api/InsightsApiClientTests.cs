using System.Net;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Contracts.Insights;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Feature 020 operations of the typed client (backend contract 1.19.0): method, route, body and result.</summary>
public sealed class InsightsApiClientTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid Track = TestData.TrackId;
    private readonly StubHttpMessageHandler _handler = new();
    private readonly StemStudioApiClient _client;

    public InsightsApiClientTests() => _client = new StemStudioApiClient(StubHttpMessageHandler.CreateClient(_handler));

    public void Dispose() => _handler.Dispose();

    private CapturedRequest Single(HttpMethod method, string path)
    {
        var request = _handler.Requests.ShouldHaveSingleItem();
        (request.Method, request.PathAndQuery).ShouldBe((method, path));
        _handler.Requests.Clear();
        return request;
    }

    [Fact]
    public async Task Quality_GetAndRequest_ReadTheContractExample()
    {
        _handler.Respond(HttpStatusCode.OK, InsightsData.ReadyReportJson);
        var report = await _client.GetQualityReportAsync(Track, Ct);
        (report.Status, report.Stems[1].Rating, report.Stems[0].Tips[0].Action!.Kind).ShouldBe(
            (QualityReportStatusDto.Ready, QualityRatingDto.NotPresent, TipActionKindDto.SplitDetail));
        Single(HttpMethod.Get, $"/api/tracks/{Track}/quality");

        _handler.Respond(HttpStatusCode.Accepted, InsightsData.ToJson(InsightsData.Report(QualityReportStatusDto.Queued)));
        (await _client.RequestQualityReportAsync(Track, Ct)).Status.ShouldBe(QualityReportStatusDto.Queued);
        Single(HttpMethod.Post, $"/api/tracks/{Track}/quality").Body.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task Quality_Running_ThrowsWithTheProblemCode()
    {
        _handler.RespondProblem(HttpStatusCode.Conflict, """{"type":"https://stemstudio.local/problems/quality-running","title":"Quality report already running","status":409}""");

        var error = (await Should.ThrowAsync<ApiException>(() => _client.RequestQualityReportAsync(Track, Ct))).Error;

        (error.Code, error.IsConflict).ShouldBe(("quality-running", true));
    }

    [Fact]
    public async Task HandoutOptions_UseTheSongOrSetlistQuery_AndTheTransposition()
    {
        _handler.Respond(HttpStatusCode.OK, InsightsData.BassOptionsJson);
        var options = await _client.GetHandoutOptionsAsync(HandoutSourceDto.Song(Track), HandoutFamilyDto.Bass, cancellationToken: Ct);
        (options.Ingredients[3].Action, options.Notation[0].AssetId, options.EstimateSeconds).ShouldBe((IngredientActionDto.CreateTab, InsightsData.BassScoreId, 6));
        Single(HttpMethod.Get, $"/api/handouts/options?trackId={Track}&family=bass");

        _handler.Respond(HttpStatusCode.OK, InsightsData.BassOptionsJson);
        await _client.GetHandoutOptionsAsync(HandoutSourceDto.Setlist(InsightsData.SetlistId), HandoutFamilyDto.Melodic, TranspositionDto.BFlat, Ct);
        Single(HttpMethod.Get, $"/api/handouts/options?setlistId={InsightsData.SetlistId}&family=melodic&transposition=b-flat");
    }

    [Fact]
    public async Task CreateHandout_PostsTheOneClickBody_WithoutNulls()
    {
        _handler.Respond(HttpStatusCode.Accepted, InsightsData.ToJson(InsightsData.Handout()));

        var asset = await _client.CreateHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Song(Track), Family = HandoutFamilyDto.Bass }, Ct);

        (asset.Kind, asset.Status).ShouldBe((AssetKindDto.Handout, AssetStatusDto.Queued));
        var body = Single(HttpMethod.Post, "/api/assets/handouts").Body!;
        body.ShouldContain($"\"source\":{{\"trackId\":\"{Track}\"}}");
        body.ShouldContain("\"family\":\"bass\"");
    }

    [Fact]
    public async Task PracticeNotes_GetSetAndDelete()
    {
        _handler.Respond(HttpStatusCode.OK, InsightsData.ToJson(new PracticeNotesDto { Notes = [InsightsData.Note(HandoutFamilyDto.Bass, "Walk the bass line.")] }));
        (await _client.GetPracticeNotesAsync(Track, Ct)).Notes.ShouldHaveSingleItem().Text.ShouldBe("Walk the bass line.");
        Single(HttpMethod.Get, $"/api/tracks/{Track}/practice-notes");

        _handler.Respond(HttpStatusCode.OK, InsightsData.ToJson(InsightsData.Note(HandoutFamilyDto.Melodic, "Long tones.")));
        (await _client.SetPracticeNoteAsync(Track, HandoutFamilyDto.Melodic, "Long tones.", Ct))!.Text.ShouldBe("Long tones.");
        Single(HttpMethod.Put, $"/api/tracks/{Track}/practice-notes/melodic").Body.ShouldBe("""{"text":"Long tones."}""");

        _handler.Respond(HttpStatusCode.NoContent);
        (await _client.SetPracticeNoteAsync(Track, HandoutFamilyDto.Melodic, " ", Ct)).ShouldBeNull();
        Single(HttpMethod.Put, $"/api/tracks/{Track}/practice-notes/melodic");
    }
}
