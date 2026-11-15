using System.Net;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Detail phase operations of the typed client (backend contract 1.2.0); shares the fixture of the client tests.</summary>
public sealed partial class StemStudioApiClientTests
{
    [Fact]
    public async Task StartDetail_PostsToDetailRoute_AndReturnsQueuedTrack()
    {
        _handler.Respond(HttpStatusCode.Accepted, TestData.DetailQueuedTrackJson);

        var track = await _client.StartDetailAsync(TestData.TrackId, Ct);

        track.Detail.Status.ShouldBe(DetailPhaseStatusDto.Queued);
        track.Detail.Stage.ShouldBe("Queued");
        track.Detail.Parents.ShouldBeEmpty();
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.PathAndQuery.ShouldBe($"/api/tracks/{TestData.TrackId}/detail");
        request.Body.ShouldBeNull();
    }

    [Fact]
    public async Task CancelDetail_PostsToCancelRoute_AndReturnsCancelledTrack()
    {
        _handler.Respond(HttpStatusCode.OK, TestData.DetailQueuedTrackJson.Replace("\"status\": \"queued\"", "\"status\": \"cancelled\"", StringComparison.Ordinal));

        var track = await _client.CancelDetailAsync(TestData.TrackId, Ct);

        track.Detail.Status.ShouldBe(DetailPhaseStatusDto.Cancelled);
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.PathAndQuery.ShouldBe($"/api/tracks/{TestData.TrackId}/detail/cancel");
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "Detail phase not startable", "The detail phase is already completed.")]
    [InlineData(HttpStatusCode.NotFound, "Not found", "Track 0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a does not exist.")]
    public async Task StartDetail_Problem_ThrowsApiExceptionWithReadableDetail(HttpStatusCode status, string title, string detail)
    {
        _handler.RespondProblem(status, $$"""{"title":"{{title}}","status":{{(int)status}},"detail":"{{detail}}"}""");

        var error = (await Should.ThrowAsync<ApiException>(() => _client.StartDetailAsync(TestData.TrackId, Ct))).Error;

        error.Status.ShouldBe(status);
        error.UserMessage.ShouldBe(detail);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "The detail phase is not queued or running.")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "The processing service is unavailable. Existing tracks and mixes remain usable.")]
    public async Task CancelDetail_Problem_ThrowsApiExceptionWithReadableDetail(HttpStatusCode status, string detail)
    {
        _handler.RespondProblem(status, $$"""{"title":"Problem","status":{{(int)status}},"detail":"{{detail}}"}""");

        var error = (await Should.ThrowAsync<ApiException>(() => _client.CancelDetailAsync(TestData.TrackId, Ct))).Error;

        error.Status.ShouldBe(status);
        error.IsConflict.ShouldBe(status == HttpStatusCode.Conflict);
        error.IsUnavailable.ShouldBe(status == HttpStatusCode.ServiceUnavailable);
        error.UserMessage.ShouldBe(detail);
    }

    [Fact]
    public async Task TrackDetail_None_IsDeserialized()
    {
        _handler.Respond(HttpStatusCode.OK, TestData.DetailNoneTrackJson);

        var track = await _client.GetTrackAsync(TestData.TrackId, Ct);

        track.Detail.Status.ShouldBe(DetailPhaseStatusDto.None);
        track.Detail.Progress.ShouldBe(0);
        track.Detail.Parents.ShouldBeEmpty();
        track.Detail.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task TrackWithoutDetailMember_DefaultsToNone()
    {
        _handler.Respond(HttpStatusCode.OK, TestData.CompletedTrackJson);

        (await _client.GetTrackAsync(TestData.TrackId, Ct)).Detail.ShouldBe(DetailPhaseDto.None);
    }

    [Fact]
    public async Task TrackDetail_Completed_DeserializesOutcomesAndDetailStems()
    {
        _handler.Respond(HttpStatusCode.OK, TestData.DetailCompletedTrackJson);

        var detail = (await _client.GetTrackAsync(TestData.TrackId, Ct)).Detail;

        (detail.Status, detail.Progress, detail.UpdatedAt).ShouldBe((DetailPhaseStatusDto.Completed, 100, new DateTimeOffset(2026, 11, 15, 12, 31, 10, TimeSpan.Zero)));
        detail.Parents.Select(p => (p.Parent, p.Outcome)).ShouldBe(
        [
            ("vocals", ParentOutcomeKindDto.Split),
            ("drums", ParentOutcomeKindDto.Split),
            ("bass", ParentOutcomeKindDto.NotSupported),
            ("other", ParentOutcomeKindDto.SkippedSilent),
        ]);
        detail.Parents[1].Stems.ShouldBe([TestData.DetailStem("drums", "kick"), TestData.DetailStem("drums", "snare"), TestData.DetailStem("drums", "rest")]);
        detail.Parents[3].Stems.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("running", DetailPhaseStatusDto.Running)]
    [InlineData("failed", DetailPhaseStatusDto.Failed)]
    public async Task TrackDetail_RunningAndFailed_CarryStageParentAndError(string status, DetailPhaseStatusDto expected)
    {
        var json = TestData.DetailQueuedTrackJson
            .Replace("\"status\": \"queued\"", $"\"status\": \"{status}\"", StringComparison.Ordinal)
            .Replace("\"currentParent\": null", "\"currentParent\": \"drums\"", StringComparison.Ordinal)
            .Replace("\"error\": null, \"parents\"", "\"error\": \"interrupted by restart\", \"parents\"", StringComparison.Ordinal);
        _handler.Respond(HttpStatusCode.OK, json);

        var detail = (await _client.GetTrackAsync(TestData.TrackId, Ct)).Detail;

        detail.Status.ShouldBe(expected);
        detail.CurrentParent.ShouldBe("drums");
        detail.Error.ShouldBe("interrupted by restart");
    }
}
