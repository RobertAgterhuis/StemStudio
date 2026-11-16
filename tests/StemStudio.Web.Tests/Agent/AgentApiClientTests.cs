using System.Net;
using System.Text.Json;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Agent;

/// <summary>Every <c>/api/agent/*</c> call of DELTA.md section 1 (backend API 1.10.0) and the event stream.</summary>
public sealed class AgentApiClientTests : IDisposable
{
    private static readonly string Run = AgentData.RunId.ToString();
    private static readonly string Conv = AgentData.ConversationId.ToString();
    private static readonly string Step = AgentData.StepId.ToString();
    private readonly SseHttpHandler _handler = new();
    private readonly AgentApiClient _client;

    public AgentApiClientTests() => _client = new AgentApiClient(SseHttpHandler.CreateClient(_handler)) { ReconnectDelay = TimeSpan.Zero };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _handler.Dispose();

    [Fact]
    public async Task StatusAndSettings_UseTheirRoutes()
    {
        _handler.Json(HttpStatusCode.OK, AgentData.Serialize(AgentData.Status()))
            .Json(HttpStatusCode.OK, AgentData.Serialize(AgentData.Settings()))
            .Json(HttpStatusCode.OK, AgentData.Serialize(AgentData.Settings(AgentBackendDto.Claude, true)));

        var status = await _client.GetStatusAsync(Ct);
        var settings = await _client.GetSettingsAsync(Ct);
        var updated = await _client.UpdateSettingsAsync(new UpdateAgentSettingsRequest { Backend = AgentBackendDto.Claude, AcknowledgeClaudeNotice = true }, Ct);

        (status.Ready, status.ActiveModel, status.Ollama.Models.Count).ShouldBe((true, "qwen3.5:9b", 3));
        settings.OllamaModel.ShouldBe("qwen3.5:9b");
        updated.Backend.ShouldBe(AgentBackendDto.Claude);
        _handler.Requests.Select(r => (r.Method, r.PathAndQuery)).ShouldBe(
            [(HttpMethod.Get, "/api/agent/status"), (HttpMethod.Get, "/api/agent/settings"), (HttpMethod.Put, "/api/agent/settings")]);
        _handler.Requests[2].Body.ShouldBe("""{"backend":"claude","acknowledgeClaudeNotice":true}""");
    }

    [Fact]
    public async Task TestBackend_PostsTheBackend()
    {
        _handler.Json(HttpStatusCode.OK, """{"backend":"ollama","ok":true,"latencyMs":42,"detail":"qwen3.5:9b ready"}""");

        var result = await _client.TestBackendAsync(AgentBackendDto.Ollama, Ct);

        (result.Ok, result.LatencyMs, result.Detail).ShouldBe((true, 42, "qwen3.5:9b ready"));
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery, sent.Body).ShouldBe((HttpMethod.Post, "/api/agent/settings/test", """{"backend":"ollama"}"""));
    }

    [Theory]
    [InlineData(null, false, "/api/agent/tools")]
    [InlineData("mixer", false, "/api/agent/tools?page=mixer")]
    [InlineData("mixer", true, "/api/agent/tools?page=mixer&trackId=e1b2c3d4-0000-4000-8000-000000000001")]
    [InlineData(null, true, "/api/agent/tools?trackId=e1b2c3d4-0000-4000-8000-000000000001")]
    public async Task ListTools_SendsPageAndTrack(string? page, bool withTrack, string expected)
    {
        _handler.Json(HttpStatusCode.OK, """[{"name":"search_library","feature":"001","kind":"server","risk":"read","undo":"none","available":true,"unavailableReason":null,"title":"Search library","description":"Finds tracks"}]""");

        var tools = await _client.ListToolsAsync(page, withTrack ? AgentData.TrackId : null, Ct);

        tools.ShouldHaveSingleItem().Risk.ShouldBe(ToolRiskDto.Read);
        _handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe(expected);
    }

    [Fact]
    public async Task ListConversations_SendsLimitAndBefore()
    {
        _handler.Json(HttpStatusCode.OK, AgentData.Serialize(new[] { AgentData.Summary() })).Json(HttpStatusCode.OK, "[]");

        var list = await _client.ListConversationsAsync(cancellationToken: Ct);
        await _client.ListConversationsAsync(10, AgentData.At, Ct);

        list.ShouldHaveSingleItem().Title.ShouldBe("Mix practice");
        _handler.Requests.Select(r => r.PathAndQuery).ShouldBe(
            ["/api/agent/conversations?limit=50", "/api/agent/conversations?limit=10&before=2026-11-16T10%3A00%3A00.0000000%2B00%3A00"]);
    }

    [Fact]
    public async Task ConversationCrud_UsesTheContractRoutes()
    {
        var conversation = AgentData.Serialize(AgentData.Conversation());
        _handler.Json(HttpStatusCode.Created, conversation).Json(HttpStatusCode.OK, conversation)
            .Json(HttpStatusCode.OK, AgentData.Serialize(AgentData.Summary(title: "Renamed"))).Json(HttpStatusCode.NoContent);

        (await _client.CreateConversationAsync(null, Ct)).ConversationId.ShouldBe(AgentData.ConversationId);
        (await _client.GetConversationAsync(AgentData.ConversationId, Ct)).Messages.ShouldBeEmpty();
        (await _client.RenameConversationAsync(AgentData.ConversationId, "Renamed", Ct)).Title.ShouldBe("Renamed");
        await _client.DeleteConversationAsync(AgentData.ConversationId, Ct);

        _handler.Requests.Select(r => (r.Method, r.PathAndQuery, r.Body)).ShouldBe(
        [
            (HttpMethod.Post, "/api/agent/conversations", "{}"),
            (HttpMethod.Get, $"/api/agent/conversations/{Conv}", null),
            (HttpMethod.Patch, $"/api/agent/conversations/{Conv}", """{"title":"Renamed"}"""),
            (HttpMethod.Delete, $"/api/agent/conversations/{Conv}", null),
        ]);
    }

    [Fact]
    public async Task SendMessageAndUndo_PostToTheConversation()
    {
        _handler.Json(HttpStatusCode.Accepted, $$"""{"runId":"{{Run}}","messageId":"{{AgentData.MessageId}}"}""").Json(HttpStatusCode.Accepted, $$"""{"runId":"{{Run}}"}""");
        var request = new SendAgentMessageRequest { Text = "Zoek Hero of the Day", Context = new PageContextDto { Page = "mixer", TrackId = AgentData.TrackId } };

        var accepted = await _client.SendMessageAsync(AgentData.ConversationId, request, Ct);
        var undo = await _client.UndoAsync(AgentData.ConversationId, Ct);

        (accepted.RunId, accepted.MessageId, undo.RunId).ShouldBe((AgentData.RunId, AgentData.MessageId, AgentData.RunId));
        _handler.Requests[0].PathAndQuery.ShouldBe($"/api/agent/conversations/{Conv}/messages");
        _handler.Requests[0].Body.ShouldBe($$"""{"text":"Zoek Hero of the Day","context":{"page":"mixer","trackId":"{{AgentData.TrackId}}"}}""");
        (_handler.Requests[1].Method, _handler.Requests[1].PathAndQuery).ShouldBe((HttpMethod.Post, $"/api/agent/conversations/{Conv}/undo"));
    }

    [Fact]
    public async Task RunOperations_UseTheRunRoutes()
    {
        var run = AgentData.Serialize(AgentData.Run(AgentRunStatusDto.Cancelled));
        _handler.Json(HttpStatusCode.OK, run)
            .Json(HttpStatusCode.OK, $$"""{"stepId":"{{Step}}","status":"approved","scope":"plan_heavy","decidedAt":"2026-11-16T10:00:05Z"}""")
            .Json(HttpStatusCode.NoContent)
            .Json(HttpStatusCode.Accepted, run);

        (await _client.GetRunAsync(AgentData.RunId, Ct)).Status.ShouldBe(AgentRunStatusDto.Cancelled);
        var decision = await _client.DecideApprovalAsync(
            AgentData.RunId, AgentData.StepId, new DecideApprovalRequest { Decision = ApprovalDecisionDto.Approve, Scope = ApprovalScopeDto.PlanHeavy, ArgumentsHash = "sha256:abc" }, Ct);
        await _client.SubmitUiResultAsync(AgentData.RunId, AgentData.StepId, new SubmitUiResultRequest { Ok = true, Result = AgentData.Json("""{"stem":"vocals"}""") }, Ct);
        await _client.CancelRunAsync(AgentData.RunId, true, Ct);

        decision.Scope.ShouldBe(ApprovalScopeDto.PlanHeavy);
        _handler.Requests.Select(r => (r.Method, r.PathAndQuery, r.Body)).ShouldBe(
        [
            (HttpMethod.Get, $"/api/agent/runs/{Run}", null),
            (HttpMethod.Post, $"/api/agent/runs/{Run}/approvals/{Step}", """{"decision":"approve","scope":"plan_heavy","argumentsHash":"sha256:abc"}"""),
            (HttpMethod.Post, $"/api/agent/runs/{Run}/ui-results/{Step}", """{"ok":true,"result":{"stem":"vocals"}}"""),
            (HttpMethod.Post, $"/api/agent/runs/{Run}/cancel", """{"cancelJobs":true}"""),
        ]);
    }

    [Fact]
    public async Task Problem_IsMappedToApiException_WithTypeCodeAndAgentStatus()
    {
        var status = AgentData.Serialize(AgentData.Status(ready: false));
        _handler.Problem(HttpStatusCode.ServiceUnavailable, $$"""{"type":"https://stemstudio.local/problems/backend-unavailable","title":"The agent backend is not ready.","status":503,"detail":"Start Ollama","agentStatus":{{status}}}""");

        var ex = await Should.ThrowAsync<ApiException>(() => _client.SendMessageAsync(AgentData.ConversationId, new SendAgentMessageRequest { Text = "x", Context = new PageContextDto { Page = "tracks" } }, Ct));

        ex.Error.IsUnavailable.ShouldBeTrue();
        (ex.Error.Code, ex.Error.UserMessage).ShouldBe((AgentProblemTypes.BackendUnavailable, "Start Ollama"));
        AgentProblems.StatusOf(ex.Error).ShouldNotBeNull().Reason.ShouldBe("Ollama not reachable at http://host.docker.internal:11434");
    }

    [Fact]
    public async Task ProblemWithoutAgentStatus_HasNoStatus_AndAConflictCode()
    {
        _handler.Problem(HttpStatusCode.Conflict, """{"type":"https://stemstudio.local/problems/run-active","title":"A run is active.","status":409}""");

        var ex = await Should.ThrowAsync<ApiException>(() => _client.DeleteConversationAsync(AgentData.ConversationId, Ct));

        (ex.Error.IsConflict, ex.Error.Code).ShouldBe((true, AgentProblemTypes.RunActive));
        AgentProblems.StatusOf(ex.Error).ShouldBeNull();
    }

    [Fact]
    public async Task Unreachable_IsMappedToTheUnreachableError()
    {
        _handler.Unreachable();

        var ex = await Should.ThrowAsync<ApiException>(() => _client.GetStatusAsync(Ct));

        ex.Error.ShouldBeSameAs(ApiError.Unreachable);
    }

    [Fact]
    public async Task Stream_ParsesARecordedStream_UntilTheTerminalEvent()
    {
        _handler.Events(SseHttpHandler.Fixture("simple-run.sse"));

        var events = await CollectAsync(_client.StreamRunEventsAsync(AgentData.RunId, null, Ct));

        events.Select(e => e.Id).ShouldBe([1, 2, 3, 4, 5, 6, 7]);
        events[0].Read<RunStartedEventDto>().Model.ShouldBe("qwen3.5:9b");
        events[2].Read<AgentStepDto>().Tool.ShouldBe("search_library");
        events[^1].IsTerminal.ShouldBeTrue();
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.PathAndQuery, sent.Accept, sent.LastEventId).ShouldBe(($"/api/agent/runs/{Run}/events", "text/event-stream", null));
    }

    [Fact]
    public async Task Stream_SendsAfter_OnTheFirstConnection()
    {
        _handler.Events(SseHttpHandler.Fixture("interrupted-run.sse"));

        var events = await CollectAsync(_client.StreamRunEventsAsync(AgentData.RunId, 12, Ct));

        events.ShouldHaveSingleItem().Type.ShouldBe(AgentEventTypes.RunInterrupted);
        _handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe($"/api/agent/runs/{Run}/events?after=12");
    }

    [Fact]
    public async Task Stream_ReconnectsWithLastEventId_AfterADroppedConnection()
    {
        _handler.Broken(SseHttpHandler.Fixture("partial-run.sse")).Events(SseHttpHandler.Fixture("resumed-run.sse"));

        var events = await CollectAsync(_client.StreamRunEventsAsync(AgentData.RunId, null, Ct));

        events.Select(e => e.Type).ShouldBe([AgentEventTypes.RunStarted, AgentEventTypes.TextDelta, AgentEventTypes.RunCancelled]);
        _handler.Requests.Select(r => (r.PathAndQuery, r.LastEventId)).ShouldBe(
            [($"/api/agent/runs/{Run}/events", null), ($"/api/agent/runs/{Run}/events", "2")]);
    }

    [Fact]
    public async Task Stream_ReconnectsWithAfter_WhenTheStreamEndsEarlyOrTheConnectFails()
    {
        _handler.Events(string.Empty).Unreachable().Events(SseHttpHandler.Fixture("interrupted-run.sse"));

        var events = await CollectAsync(_client.StreamRunEventsAsync(AgentData.RunId, 7, Ct));

        events.ShouldHaveSingleItem().Type.ShouldBe(AgentEventTypes.RunInterrupted);
        _handler.Requests.Select(r => r.LastEventId).ShouldBe([null, "7", "7"]);
    }

    [Fact]
    public async Task Stream_GivesUp_AfterRepeatedFailures()
    {
        for (var i = 0; i <= AgentApiClient.MaxReconnects; i++)
        {
            _handler.Unreachable();
        }

        var ex = await Should.ThrowAsync<ApiException>(() => CollectAsync(_client.StreamRunEventsAsync(AgentData.RunId, null, Ct)));

        ex.Error.ShouldBeSameAs(ApiError.Unreachable);
        _handler.Requests.Count.ShouldBe(AgentApiClient.MaxReconnects + 1);
    }

    [Fact]
    public async Task Stream_NotFound_ThrowsWithoutRetrying()
    {
        _handler.Problem(HttpStatusCode.NotFound, """{"title":"Run not found.","status":404}""");

        var ex = await Should.ThrowAsync<ApiException>(() => CollectAsync(_client.StreamRunEventsAsync(AgentData.RunId, null, Ct)));

        ex.Error.IsNotFound.ShouldBeTrue();
        _handler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public void Event_ReadsItsDataWithTheContractOptions()
    {
        var e = new AgentEvent(3, AgentEventTypes.StepUpdated, AgentData.Json($$"""{"stepId":"{{Step}}","status":"waiting_job","progress":40}"""));

        var update = e.Read<StepUpdatedEventDto>();

        (update.Status, update.Progress, e.IsTerminal).ShouldBe((AgentStepStatusDto.WaitingJob, 40, false));
    }

    [Fact]
    public void Event_WithNullData_Throws()
    {
        var e = new AgentEvent(3, AgentEventTypes.TextDelta, JsonDocument.Parse("null").RootElement);

        Should.Throw<JsonException>(() => e.Read<TextDeltaEventDto>());
    }

    private static async Task<List<AgentEvent>> CollectAsync(IAsyncEnumerable<AgentEvent> stream)
    {
        var events = new List<AgentEvent>();
        await foreach (var e in stream)
        {
            events.Add(e);
        }

        return events;
    }
}
