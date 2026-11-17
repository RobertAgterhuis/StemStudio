using System.Net;
using System.Text.Json;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Songs;

/// <summary>The lyric-assistant calls of backend-api 1.23.0: routes, methods, bodies, the contract samples and the event stream.</summary>
public sealed class LyricAssistApiClientTests : IDisposable
{
    private static readonly Guid Sheet = LyricAssistData.LyricsId;
    private static readonly Guid Assist = LyricAssistData.AssistId;
    private readonly SseHttpHandler _handler = new();
    private readonly StemStudioApiClient _client;

    public LyricAssistApiClientTests() => _client = new StemStudioApiClient(SseHttpHandler.CreateClient(_handler));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _handler.Dispose();

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, LyricAssistData.Web);

    private CapturedAgentRequest Sent => _handler.Requests.ShouldHaveSingleItem();

    [Fact]
    public async Task ContractSamples_Deserialise()
    {
        _handler.Json(HttpStatusCode.OK, LyricAssistData.Sample("assist")).Json(HttpStatusCode.OK, $$"""{"lyricsId":"{{Sheet}}","revision":3,"suggestions":[{{LyricAssistData.Sample("suggestion")}}]}""")
            .Json(HttpStatusCode.OK, $"[{LyricAssistData.Sample("summary")}]");

        var assist = await _client.GetLyricAssistAsync(Assist, Ct);
        var list = await _client.ListLyricSuggestionsAsync(Sheet, null, Ct);
        var history = await _client.ListLyricAssistsAsync(Sheet, Ct);

        assist.AssistId.ShouldBe(Assist);
        list.Suggestions.ShouldHaveSingleItem().RhymeWith!.Word.ShouldBe("nacht");
        history.ShouldHaveSingleItem().Status.ShouldBe(AssistStatusDto.Failed);
        _handler.Requests.Select(r => r.PathAndQuery).ShouldBe([$"/api/lyric-assists/{Assist}", $"/api/lyrics/{Sheet}/suggestions", $"/api/lyrics/{Sheet}/assists"]);
    }

    [Fact]
    public async Task StatusAndSettings_UseTheirRoutes_AndReadingSettingsIsAGet()
    {
        _handler.Json(HttpStatusCode.OK, Json(LyricAssistData.Status())).Json(HttpStatusCode.OK, Json(LyricAssistData.Settings()))
            .Json(HttpStatusCode.OK, Json(LyricAssistData.Settings()));

        (await _client.GetLyricAssistStatusAsync(Ct)).Model.ActiveModel.ShouldBe("qwen3.5:9b");
        (await _client.GetLyricAssistSettingsAsync(Ct)).OwnWorkStatementVersion.ShouldBe("own-work.v1");
        await _client.UpdateLyricAssistSettingsAsync(new LyricAssistSettingsUpdate { AcknowledgeClaudeNotice = true }, Ct);

        _handler.Requests.Select(r => (r.Method, r.PathAndQuery)).ShouldBe(
            [(HttpMethod.Get, "/api/lyric-assist/status"), (HttpMethod.Get, "/api/lyric-assist/settings"), (HttpMethod.Put, "/api/lyric-assist/settings")]);
        _handler.Requests[2].Body.ShouldBe("""{"acknowledgeClaudeNotice":true}""");
    }

    [Fact]
    public async Task CreateAssist_PostsTheRequest()
    {
        _handler.Json(HttpStatusCode.Accepted, Json(LyricAssistData.Assist()));

        await _client.CreateLyricAssistAsync(Sheet, new CreateLyricAssistRequest { Kind = AssistKindDto.Alternatives, ExpectedRevision = 3, LineFrom = 2, LineTo = 2, Count = 3 }, Ct);

        (Sent.Method, Sent.PathAndQuery).ShouldBe((HttpMethod.Post, $"/api/lyrics/{Sheet}/assists"));
        using var body = JsonDocument.Parse(Sent.Body!);
        (body.RootElement.GetProperty("kind").GetString(), body.RootElement.GetProperty("count").GetInt32()).ShouldBe(("alternatives", 3));
    }

    [Fact]
    public async Task SheetSettings_AlwaysSendTheRhythmTarget()
    {
        _handler.Json(HttpStatusCode.OK, Json(LyricAssistData.Sheet().Assist)).Json(HttpStatusCode.OK, Json(LyricAssistData.Sheet().Assist));

        await _client.UpdateSheetAssistSettingsAsync(Sheet, new SheetAssistSettingsUpdate { Sensitivity = SensitivityDto.Strict, RhythmTarget = null }, Ct);
        await _client.UpdateSheetAssistSettingsAsync(Sheet, new SheetAssistSettingsUpdate { RhythmTarget = new RhythmTargetDto { Bpm = 92, BeatsPerBar = 4, BarsPerLine = 2, SyllablesPerBeat = 1.5 } }, Ct);

        _handler.Requests[0].Body.ShouldBe("""{"sensitivity":"strict","rhythmTarget":null}""");
        _handler.Requests[1].Body.ShouldBe("""{"rhythmTarget":{"bpm":92,"beatsPerBar":4,"barsPerLine":2,"syllablesPerBeat":1.5}}""");
        _handler.Requests[0].PathAndQuery.ShouldBe($"/api/lyrics/{Sheet}/assist-settings");
    }

    [Fact]
    public async Task Decisions_PostIdsAndRevision()
    {
        var accepted = Json(LyricAssistData.Accepted("x"));
        _handler.Json(HttpStatusCode.OK, accepted).Json(HttpStatusCode.OK, Json(LyricAssistData.List())).Json(HttpStatusCode.OK, accepted)
            .Json(HttpStatusCode.OK, accepted);
        var decision = new SuggestionDecisionRequest { SuggestionIds = [LyricAssistData.Spelling], ExpectedRevision = 3 };

        await _client.AcceptLyricSuggestionsAsync(Sheet, decision, Ct);
        await _client.RejectLyricSuggestionsAsync(Sheet, decision, Ct);
        await _client.UndoLyricSuggestionsAsync(Sheet, 4, Ct);
        await _client.RestoreLyricsRevisionAsync(Sheet, 2, 5, Ct);

        _handler.Requests.Select(r => r.PathAndQuery).ShouldBe([
            $"/api/lyrics/{Sheet}/suggestions/accept", $"/api/lyrics/{Sheet}/suggestions/reject", $"/api/lyrics/{Sheet}/undo", $"/api/lyrics/{Sheet}/revisions/2/restore"]);
        _handler.Requests[0].Body.ShouldBe($$"""{"suggestionIds":["{{LyricAssistData.Spelling}}"],"expectedRevision":3}""");
        _handler.Requests[2].Body.ShouldBe("""{"expectedRevision":4}""");
        _handler.Requests[3].Body.ShouldBe("""{"expectedRevision":5}""");
    }

    [Fact]
    public async Task MarksWordsAndOverride_UseTheirRoutes()
    {
        var mark = Json(new IntentionalMarkDto { MarkId = Guid.Empty, Scope = IntentionalScopeDto.Word, Text = "wiet", InText = true, CreatedAt = DateTimeOffset.UnixEpoch });
        var word = Json(new LyricWordDto { WordId = Guid.Empty, Language = AssistLanguageDto.Nl, Word = "Zeeland", CreatedAt = DateTimeOffset.UnixEpoch });
        _handler.Json(HttpStatusCode.Created, mark).Json(HttpStatusCode.OK, $"[{mark}]").Json(HttpStatusCode.Created, mark).Json(HttpStatusCode.NoContent)
            .Json(HttpStatusCode.OK, $"[{word}]").Json(HttpStatusCode.Created, word).Json(HttpStatusCode.NoContent)
            .Json(HttpStatusCode.OK, Json(LyricAssistData.Sheet().Assist.OwnWorkOverride ?? new OwnWorkOverrideDto
            {
                StatementVersion = "own-work.v1",
                ConfirmedBy = "Robin",
                ConfirmedAt = DateTimeOffset.UnixEpoch,
                MatchedTrackId = LyricAssistData.TrackId,
                MatchedTrackTitle = "Fake Song",
            })).Json(HttpStatusCode.NoContent);

        await _client.MarkSuggestionIntentionalAsync(Sheet, LyricAssistData.Spelling, new MarkSuggestionIntentionalRequest { Scope = IntentionalScopeDto.Word, ExpectedRevision = 3 }, Ct);
        await _client.ListIntentionalMarksAsync(Sheet, Ct);
        await _client.CreateIntentionalMarkAsync(Sheet, new IntentionalMarkInput { Scope = IntentionalScopeDto.Phrase, Text = "Gij zijt" }, Ct);
        await _client.DeleteIntentionalMarkAsync(Sheet, Guid.Empty, Ct);
        await _client.ListLyricWordsAsync(AssistLanguageDto.Nl, Ct);
        await _client.AddLyricWordAsync(new LyricWordInput { Language = AssistLanguageDto.Nl, Word = "Zeeland" }, Ct);
        await _client.DeleteLyricWordAsync(Guid.Empty, Ct);
        await _client.ConfirmOwnWorkAsync(Sheet, new OwnWorkConfirmRequest { MatchedTrackId = LyricAssistData.TrackId, ConfirmedBy = "Robin", StatementVersion = "own-work.v1", Confirm = true }, Ct);
        await _client.RevokeOwnWorkAsync(Sheet, Ct);

        _handler.Requests.Select(r => $"{r.Method} {r.PathAndQuery}").ShouldBe([
            $"POST /api/lyrics/{Sheet}/suggestions/{LyricAssistData.Spelling}/intentional",
            $"GET /api/lyrics/{Sheet}/intentional-marks",
            $"POST /api/lyrics/{Sheet}/intentional-marks",
            $"DELETE /api/lyrics/{Sheet}/intentional-marks/{Guid.Empty}",
            "GET /api/lyric-assist/words?language=nl",
            "POST /api/lyric-assist/words",
            $"DELETE /api/lyric-assist/words/{Guid.Empty}",
            $"PUT /api/lyrics/{Sheet}/own-work-override",
            $"DELETE /api/lyrics/{Sheet}/own-work-override"]);
    }

    [Fact]
    public async Task LocalHelpers_UseTheirRoutes()
    {
        _handler.Json(HttpStatusCode.OK, """{"language":"nl","lines":[],"targetDurationSeconds":null,"wordsPerSecond":null}""")
            .Json(HttpStatusCode.OK, """{"word":"nacht","language":"nl","rhymes":[{"word":"lacht","kind":"perfect","syllables":1}],"source":"local"}""")
            .Json(HttpStatusCode.OK, """{"language":"nl","confidence":0.9}""")
            .Json(HttpStatusCode.OK, "[]")
            .Json(HttpStatusCode.Accepted, Json(LyricAssistData.Assist(AssistStatusDto.Cancelled)));

        await _client.CountSyllablesAsync(new SyllableRequest { Language = AssistLanguageDto.Nl, Text = "nacht" }, Ct);
        (await _client.FindRhymesAsync("nacht", AssistLanguageDto.Nl, Ct)).Rhymes.ShouldHaveSingleItem().Kind.ShouldBe(RhymeKindDto.Perfect);
        (await _client.GuessLyricsLanguageAsync("de nacht", Ct)).Language.ShouldBe(AssistLanguageDto.Nl);
        await _client.ListLyricsRevisionsAsync(Sheet, Ct);
        await _client.CancelLyricAssistAsync(Assist, Ct);

        _handler.Requests.Select(r => $"{r.Method} {r.PathAndQuery}").ShouldBe([
            "POST /api/lyric-assist/syllables", "GET /api/lyric-assist/rhymes?word=nacht&language=nl", "POST /api/lyric-assist/language-guess",
            $"GET /api/lyrics/{Sheet}/revisions", $"POST /api/lyric-assists/{Assist}/cancel"]);
        _handler.Requests[2].Body.ShouldBe("""{"text":"de nacht"}""");
    }

    [Fact]
    public async Task Events_AreReadUntilTheTerminalOne()
    {
        _handler.Events(LyricAssistData.Sse(
            (LyricAssistEventTypes.Progress, """{"blocksDone":1,"blocksSkipped":0,"blocksTotal":2}"""),
            (LyricAssistEventTypes.SuggestionAdded, LyricAssistData.Sample("suggestion")),
            (LyricAssistEventTypes.Completed, """{"counts":{"spelling":0,"grammar":0,"flow":0,"licence":0,"device":1,"alternative":0,"continuation":0,"title":0},"discarded":{"invalidAnchor":0,"invalidSchema":0,"failedVerification":0,"filtered":0,"verbatim":0},"durationMs":10}"""),
            (LyricAssistEventTypes.Progress, """{"blocksDone":2,"blocksSkipped":0,"blocksTotal":2}""")));

        var events = new List<StemStudio.Web.Components.Lyrics.Assist.LyricAssistEvent>();
        await foreach (var e in _client.StreamLyricAssistEventsAsync(Assist, 4, Ct))
        {
            events.Add(e);
        }

        events.Select(e => (e.Id, e.Type)).ShouldBe([(1, LyricAssistEventTypes.Progress), (2, LyricAssistEventTypes.SuggestionAdded), (3, LyricAssistEventTypes.Completed)]);
        events[1].Read<LyricSuggestionDto>().Proposal.ShouldBe("zacht");
        (Sent.PathAndQuery, Sent.Accept).ShouldBe(($"/api/lyric-assists/{Assist}/events?after=4", "text/event-stream"));
    }

    [Fact]
    public async Task Problem_ThrowsWithTheCodeAndExtensions()
    {
        _handler.Problem(HttpStatusCode.Conflict, """{"type":"https://stemstudio.local/problems/not-own-lyrics","title":"Not own","status":409,"code":"not-own-lyrics","matchedTrackTitle":"Fake Song","overridable":true}""");

        var ex = await Should.ThrowAsync<ApiException>(() => _client.CreateLyricAssistAsync(Sheet, new CreateLyricAssistRequest { Kind = AssistKindDto.Check, ExpectedRevision = 1 }, Ct));

        ex.Error.Code.ShouldBe("not-own-lyrics");
        ex.Error.Extensions["matchedTrackTitle"].GetString().ShouldBe("Fake Song");
    }
}
