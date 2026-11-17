using System.Net;
using StemStudio.Contracts.Practice;
using StemStudio.Contracts.Sections;
using StemStudio.Contracts.Setlists;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Feature 014 operations of the typed client (backend contract 1.13.0): method, route, body and result per operation.</summary>
public sealed class PracticeFlowApiClientTests : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid Track = TestData.TrackId;
    private readonly StubHttpMessageHandler _handler = new();
    private readonly StemStudioApiClient _client;

    public PracticeFlowApiClientTests() => _client = new StemStudioApiClient(StubHttpMessageHandler.CreateClient(_handler));

    public void Dispose() => _handler.Dispose();

    private CapturedRequest Single(HttpMethod method, string path)
    {
        var request = _handler.Requests.ShouldHaveSingleItem();
        (request.Method, request.PathAndQuery).ShouldBe((method, path));
        return request;
    }

    [Fact]
    public async Task Sections_GetReplaceAndDetect()
    {
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(PracticeFlowData.SectionSet()));
        (await _client.GetSectionsAsync(Track, Ct)).Sections.Count.ShouldBe(5);
        Single(HttpMethod.Get, $"/api/tracks/{Track}/sections");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(PracticeFlowData.SectionSet(revision: 2)));
        var request = new SectionsUpdateRequest { Revision = 1, Sections = [new SectionInputDto { Kind = SectionKindDto.Intro, Label = "Intro", StartSeconds = 0, EndSeconds = 8 }] };
        (await _client.ReplaceSectionsAsync(Track, request, Ct)).Revision.ShouldBe(2);
        Single(HttpMethod.Put, $"/api/tracks/{Track}/sections").Body!.ShouldContain("\"revision\":1");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.Accepted, PracticeFlowData.ToJson(PracticeFlowData.SectionSet(SectionSetStatusDto.Queued)));
        (await _client.DetectSectionsAsync(Track, overwrite: true, Ct)).Status.ShouldBe(SectionSetStatusDto.Queued);
        Single(HttpMethod.Post, $"/api/tracks/{Track}/sections/detect").Body.ShouldBe("""{"overwrite":true}""");
    }

    [Fact]
    public async Task Sections_Conflict_ThrowsWithTheProblemCode()
    {
        _handler.RespondProblem(HttpStatusCode.Conflict, """{"type":"https://stemstudio.local/problems/sections-changed","title":"Changed","status":409,"detail":"Another window saved first."}""");

        var error = (await Should.ThrowAsync<ApiException>(() => _client.ReplaceSectionsAsync(Track, new SectionsUpdateRequest { Revision = 1, Sections = [] }, Ct))).Error;

        (error.IsConflict, error.Code).ShouldBe((true, "sections-changed"));
    }

    [Fact]
    public async Task Scores_ListGetAndCreateView()
    {
        var asset = PracticeFlowData.ScoreAssetId;
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(new[] { PracticeFlowData.TrackScore() }));
        (await _client.ListTrackScoresAsync(Track, Ct)).Single().Name.ShouldBe("Bass part");
        Single(HttpMethod.Get, $"/api/tracks/{Track}/scores");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(PracticeFlowData.ScoreView()));
        (await _client.GetScoreViewAsync(asset, Ct)).Bars.Count.ShouldBe(3);
        Single(HttpMethod.Get, $"/api/assets/{asset}/score-view");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.Accepted, PracticeFlowData.ToJson(PracticeFlowData.ScoreView(Contracts.ScoreFollow.ScoreViewStatusDto.Queued)));
        (await _client.CreateScoreViewAsync(asset, Ct)).Pages.ShouldBeEmpty();
        Single(HttpMethod.Post, $"/api/assets/{asset}/score-view");
    }

    [Fact]
    public async Task Setlists_CrudAndEntries()
    {
        var id = PracticeFlowData.SetlistId;
        var setlist = PracticeFlowData.ToJson(PracticeFlowData.Setlist());
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(new[] { PracticeFlowData.SetlistSummary() }));
        (await _client.ListSetlistsAsync(Ct)).Single().Songs.ShouldBe(2);
        Single(HttpMethod.Get, "/api/setlists");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.Created, setlist);
        (await _client.CreateSetlistAsync(new SetlistCreateRequest { Name = "Friday rehearsal" }, Ct)).Entries.Count.ShouldBe(2);
        Single(HttpMethod.Post, "/api/setlists").Body!.ShouldContain("Friday rehearsal");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, setlist);
        await _client.GetSetlistAsync(id, cancellationToken: Ct);
        Single(HttpMethod.Get, $"/api/setlists/{id}");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, setlist);
        await _client.GetSetlistAsync(id, acknowledgeRemoved: true, Ct);
        Single(HttpMethod.Get, $"/api/setlists/{id}?acknowledgeRemoved=true");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, setlist);
        await _client.UpdateSetlistAsync(id, new SetlistUpdateRequest { Note = "x" }, Ct);
        Single(HttpMethod.Patch, $"/api/setlists/{id}");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, setlist);
        await _client.ReplaceSetlistEntriesAsync(id, new SetlistEntriesUpdateRequest { Entries = [new SetlistEntryInputDto { TrackId = Track }] }, Ct);
        Single(HttpMethod.Put, $"/api/setlists/{id}/entries").Body!.ShouldContain(Track.ToString());

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.NoContent);
        await _client.DeleteSetlistAsync(id, Ct);
        Single(HttpMethod.Delete, $"/api/setlists/{id}");
    }

    [Fact]
    public async Task PracticeLog_SettingsSessionsSummaryAndOverview()
    {
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(PracticeFlowData.LogSettings()));
        (await _client.GetPracticeLogSettingsAsync(Ct)).Enabled.ShouldBeTrue();
        Single(HttpMethod.Get, "/api/practice/settings");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(PracticeFlowData.LogSettings(enabled: false)));
        (await _client.UpdatePracticeLogSettingsAsync(new PracticeLogSettingsUpdateRequest { Enabled = false }, Ct)).Enabled.ShouldBeFalse();
        Single(HttpMethod.Put, "/api/practice/settings");

        var session = PracticeFlowData.Session();
        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(session));
        var input = new PracticeSessionInputDto { TrackId = Track, StartedAt = session.StartedAt, EndedAt = session.EndedAt, PlayedSeconds = 700 };
        (await _client.UpsertPracticeSessionAsync(session.Id, input, Ct)).PlayedSeconds.ShouldBe(700);
        Single(HttpMethod.Put, $"/api/practice/sessions/{session.Id}");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(new[] { session }));
        (await _client.ListPracticeSessionsAsync(Track, Ct)).ShouldHaveSingleItem();
        Single(HttpMethod.Get, $"/api/practice/sessions?trackId={Track}");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(Array.Empty<PracticeSessionDto>()));
        await _client.ListPracticeSessionsAsync(null, Ct);
        Single(HttpMethod.Get, "/api/practice/sessions");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, """{"deleted":3}""");
        (await _client.DeletePracticeSessionsAsync(Track, Ct)).Deleted.ShouldBe(3);
        Single(HttpMethod.Delete, $"/api/practice/sessions?trackId={Track}");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.NoContent);
        await _client.DeletePracticeSessionAsync(session.Id, Ct);
        Single(HttpMethod.Delete, $"/api/practice/sessions/{session.Id}");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(PracticeFlowData.Summary()));
        (await _client.GetPracticeSummaryAsync(Track, 30, 60, Ct)).TodaySeconds.ShouldBe(720);
        Single(HttpMethod.Get, $"/api/practice/summary?trackId={Track}&days=30&utcOffsetMinutes=60");

        _handler.Requests.Clear();
        _handler.Respond(HttpStatusCode.OK, PracticeFlowData.ToJson(new[] { PracticeFlowData.TrackTotal() }));
        (await _client.GetPracticeOverviewAsync(-120, Ct)).ShouldHaveSingleItem();
        Single(HttpMethod.Get, "/api/practice/overview?utcOffsetMinutes=-120");
    }
}
