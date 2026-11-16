using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Tests.Agent;

/// <summary>011 T032: conversations, messages, runs, the event stream, tools, UI results and cancel (FR-001, FR-002).</summary>
public sealed class AgentEndpointsTests : AgentApiTestBase
{
    [Fact]
    public async Task Conversations_CreateListAndGet()
    {
        using var created = await Client.PostAsJsonAsync(ApiRoutes.AgentConversations, new CreateConversationRequest(), Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        await ContractAssert.MatchesAsync(created, "createConversation");
        var first = await created.BodyAsync<ConversationDto>();
        created.Headers.Location!.ToString().ShouldBe(ApiRoutes.AgentConversation(first.ConversationId));
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await CreateConversationAsync("Drums");

        using var list = await Client.GetAsync(ApiRoutes.AgentConversations, Ct);
        await ContractAssert.MatchesAsync(list, "listConversations");
        (await list.BodyAsync<ConversationSummaryDto[]>()).Select(c => c.ConversationId).ShouldBe([second.ConversationId, first.ConversationId]);
        using var page = await Client.GetAsync($"{ApiRoutes.AgentConversations}?limit=1&before=2026-11-14T12:00:30Z", Ct);
        (await page.BodyAsync<ConversationSummaryDto[]>()).Single().ConversationId.ShouldBe(first.ConversationId);

        using var get = await Client.GetAsync(ApiRoutes.AgentConversation(second.ConversationId), Ct);
        await ContractAssert.MatchesAsync(get, "getConversation");
        var conversation = await get.BodyAsync<ConversationDto>();
        (conversation.Title, conversation.LastRunStatus, conversation.Messages.Count).ShouldBe(("Drums", null, 0));
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=101")]
    [InlineData("?limit=x")]
    [InlineData("?before=yesterday")]
    public async Task ListConversations_RejectsBadQueries(string query)
    {
        using var response = await Client.GetAsync(ApiRoutes.AgentConversations + query, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(response, "listConversations");
    }

    [Fact]
    public async Task CreateConversation_RejectsALongTitle()
    {
        using var response = await Client.PostAsJsonAsync(ApiRoutes.AgentConversations, new CreateConversationRequest { Title = new string('x', 121) }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(response, "createConversation");
    }

    [Fact]
    public async Task UnknownConversationAndRun_Are404()
    {
        var id = Guid.NewGuid();
        using var get = await Client.GetAsync(ApiRoutes.AgentConversation(id), Ct);
        using var send = await SendMessageAsync(id);
        using var run = await Client.GetAsync(ApiRoutes.AgentRun(id), Ct);
        using var events = await Client.GetAsync(ApiRoutes.AgentRunEvents(id), Ct);

        await ContractAssert.MatchesAsync(get, "getConversation");
        await ContractAssert.MatchesAsync(send, "sendAgentMessage");
        await ContractAssert.MatchesAsync(run, "getAgentRun");
        await ContractAssert.MatchesAsync(events, "streamAgentRunEvents");
        new[] { get, send, run, events }.ShouldAllBe(r => r.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendMessage_StoresTheMessageQueuesARunAndTheExecutorRunsIt()
    {
        var conversation = await CreateConversationAsync();
        var text = "Mute the drums and then raise the bass by three decibels in the chorus of this song please";

        using var response = await SendMessageAsync(conversation.ConversationId, text);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "sendAgentMessage");
        var accepted = await response.BodyAsync<AgentMessageAcceptedDto>();
        await Eventually.WaitUntilAsync(() => Task.FromResult(RunService.ReceivedCalls().Any()));
        await RunService.Received(1).ExecuteAsync(accepted.RunId, Arg.Any<CancellationToken>());
        var loaded = await GetConversationAsync(conversation.ConversationId);
        loaded.Title.ShouldBe(text[..60]);
        var message = loaded.Messages.Single();
        (message.MessageId, message.Seq, message.Role, message.Text, message.RunId, message.Context.Page)
            .ShouldBe((accepted.MessageId, 1, MessageRoleDto.User, text, accepted.RunId, "mixer"));
        loaded.Runs.Single().ShouldBe(new AgentRunSummaryDto { RunId = accepted.RunId, MessageId = accepted.MessageId, Status = AgentRunStatusDto.Queued, StartedAt = ApiFactory.Start });
        loaded.LastRunStatus.ShouldBe(AgentRunStatusDto.Queued);
    }

    [Theory]
    [InlineData("", "mixer")]
    [InlineData("   ", "mixer")]
    [InlineData("long", "mixer")]
    [InlineData("Mute", "")]
    [InlineData("Mute", "a-page-name-that-is-longer-than-forty-chars")]
    public async Task SendMessage_RejectsInvalidInputWith422(string text, string page)
    {
        var conversation = await CreateConversationAsync();

        using var response = await SendMessageAsync(conversation.ConversationId, text == "long" ? new string('x', 4001) : text, page);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "sendAgentMessage");
        RunService.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task SendMessage_WhileARunIsActive_Is409RunActive()
    {
        var conversation = await CreateConversationAsync();
        var other = await CreateConversationAsync();
        using var first = await SendMessageAsync(conversation.ConversationId);

        using var second = await SendMessageAsync(other.ConversationId);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(second, "sendAgentMessage");
        (await second.JsonAsync()).GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.RunActive));
    }

    [Fact]
    public async Task Executor_ClearsTheActiveRunWhenTheRunEnded()
    {
        var conversation = await CreateConversationAsync();
        RunService.ExecuteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var runs = Service<IRunStore>();
            var run = (await runs.GetRunAsync(call.Arg<Guid>(), Ct))!;
            await runs.UpdateRunAsync(run with { Status = AgentRunStatusDto.Completed, EndedAt = ApiFactory.Start }, Ct);
        });

        using var response = await SendMessageAsync(conversation.ConversationId);

        await Eventually.WaitUntilAsync(async () => (await Service<IConversationStore>().GetAsync(conversation.ConversationId, Ct))!.ActiveRunId is null);
        using var next = await SendMessageAsync(conversation.ConversationId);
        next.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Executor_FailsTheRunWhenTheAgentThrows()
    {
        var conversation = await CreateConversationAsync();
        RunService.ExecuteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom at C:\\secret"));

        using var response = await SendMessageAsync(conversation.ConversationId);
        var runId = (await response.BodyAsync<AgentMessageAcceptedDto>()).RunId;

        await Eventually.WaitUntilAsync(async () => (await Service<IRunStore>().GetRunAsync(runId, Ct))!.IsTerminal);
        var run = (await Service<IRunStore>().GetRunAsync(runId, Ct))!;
        (run.Status, run.FailureCode, run.FailureMessage).ShouldBe((AgentRunStatusDto.Failed, AgentFailureCodes.Internal, AgentRunExecutorText.InternalFailure));
        var events = await ReadStreamAsync(runId, null);
        events[^1].Event.ShouldBe(AgentEventTypes.RunFailed);
        events[^1].Data.ShouldNotContain("secret");
    }

    [Fact]
    public async Task GetRun_ReturnsTheSnapshotWithStepsAndThePendingApproval()
    {
        var runId = await StartRunAsync();
        var step = AgentRecords.Step(runId, 1, AgentStepStatusDto.AwaitingApproval);
        await Service<IRunStore>().SaveStepAsync(step, Ct);
        await Service<IRunStore>().SaveApprovalAsync(AgentRecords.Approval(step.Id, runId), Ct);
        await PublishAsync(runId, AgentEventTypes.ApprovalRequested, new ApprovalRequestDto
        {
            StepId = step.Id,
            Tool = step.Tool,
            Risk = step.Risk,
            Arguments = JsonDocument.Parse(step.Arguments).RootElement,
            ArgumentsHash = step.ArgumentsHash,
            Reasons = [AgentDecisionReasons.RiskDestructive],
            Effect = "Deletes the mix Live",
            Undoable = true,
            AllowPlanScope = false,
        });

        using var response = await Client.GetAsync(ApiRoutes.AgentRun(runId), Ct);

        await ContractAssert.MatchesAsync(response, "getAgentRun");
        var run = await response.BodyAsync<AgentRunDto>();
        (run.Status, run.Backend, run.Model, run.Plan.Count).ShouldBe((AgentRunStatusDto.Queued, AgentBackendDto.Ollama, "qwen3.5:9b", 0));
        var dto = run.Steps.Single();
        (dto.StepId, dto.Tool, dto.Arguments.GetProperty("mixId").GetString(), dto.Job!.Phase).ShouldBe((step.Id, "delete_mix", "5c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f", JobPhaseDto.Detail));
        dto.ResultSummary!.Value.GetProperty("ok").GetBoolean().ShouldBeTrue();
        (run.PendingApproval!.StepId, run.PendingApproval.Effect).ShouldBe((step.Id, "Deletes the mix Live"));
    }

    [Fact]
    public async Task Events_ReplayFromAfterOrLastEventIdAndCloseAfterTheTerminalEvent()
    {
        var runId = await StartRunAsync();
        await PublishAsync(runId, AgentEventTypes.RunStarted, new RunStartedEventDto { RunId = runId, Backend = AgentBackendDto.Ollama, Model = "qwen3.5:9b", UsingFallback = false });
        await PublishAsync(runId, AgentEventTypes.TextDelta, new TextDeltaEventDto { Text = "Muting." });
        await PublishAsync(runId, AgentEventTypes.RunCompleted, new RunCompletedEventDto { RunId = runId, MessageId = Guid.NewGuid() });

        (await ReadStreamAsync(runId, null)).Select(e => (e.Id, e.Event)).ShouldBe(
            [("1", AgentEventTypes.RunStarted), ("2", AgentEventTypes.TextDelta), ("3", AgentEventTypes.RunCompleted)]);
        (await ReadStreamAsync(runId, 1)).Select(e => e.Id).ShouldBe(["2", "3"]);
        var fromHeader = await ReadStreamAsync(runId, null, lastEventId: "2");
        fromHeader.Single().Event.ShouldBe(AgentEventTypes.RunCompleted);
        (await ReadStreamAsync(runId, 1))[0].Data.ShouldBe("""{"text":"Muting."}""");
    }

    [Fact]
    public async Task Events_FlushTheHeadersBeforeTheFirstEvent()
    {
        var runId = await StartRunAsync();

        var open = Client.GetAsync(ApiRoutes.AgentRunEvents(runId), HttpCompletionOption.ResponseHeadersRead, Ct);
        (await Task.WhenAny(open, Task.Delay(TimeSpan.FromSeconds(10), Ct))).ShouldBe(open);

        using var response = await open;
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
    }

    [Fact]
    public async Task Events_AfterWinsOverLastEventId()
    {
        var runId = await StartRunAsync();
        await PublishAsync(runId, AgentEventTypes.TextDelta, new TextDeltaEventDto { Text = "a" });
        await PublishAsync(runId, AgentEventTypes.TextDelta, new TextDeltaEventDto { Text = "b" });
        await PublishAsync(runId, AgentEventTypes.RunCompleted, new RunCompletedEventDto { RunId = runId, MessageId = Guid.NewGuid() });

        (await ReadStreamAsync(runId, 1, lastEventId: "2")).Select(e => e.Id).ShouldBe(["2", "3"]);
    }

    [Theory]
    [InlineData("cpu", "cpu")]
    [InlineData("CPU", "cpu")]
    [InlineData("cuda:0", "gpu")]
    [InlineData("NVIDIA GeForce RTX 5070 Ti", "gpu")]
    [InlineData(null, null)]
    public async Task GetRun_MapsTheApprovalDeviceToCpuOrGpu(string? device, string? expected)
    {
        var runId = await StartRunAsync();
        var step = AgentRecords.Step(runId, 1, AgentStepStatusDto.AwaitingApproval);
        await Service<IRunStore>().SaveStepAsync(step, Ct);
        await Service<IRunStore>().SaveApprovalAsync(AgentRecords.Approval(step.Id, runId), Ct);
        await PublishAsync(runId, AgentEventTypes.ApprovalRequested, new ApprovalRequestDto
        {
            StepId = step.Id,
            Tool = "split_detail",
            Risk = ToolRiskDto.Heavy,
            Arguments = JsonDocument.Parse("{}").RootElement,
            ArgumentsHash = step.ArgumentsHash,
            Reasons = [AgentDecisionReasons.RiskHeavy],
            Effect = "Splits drums",
            Undoable = true,
            EstimatedMinutes = 6,
            Device = device,
            AllowPlanScope = true,
        });

        using var response = await Client.GetAsync(ApiRoutes.AgentRun(runId), Ct);

        await ContractAssert.MatchesAsync(response, "getAgentRun");
        (await response.BodyAsync<AgentRunDto>()).PendingApproval!.Device.ShouldBe(expected);
    }

    [Theory]
    [InlineData("?after=-1", null)]
    [InlineData("?after=x", null)]
    [InlineData("", "abc")]
    public async Task Events_RejectBadReplayPositions(string query, string? lastEventId)
    {
        var runId = await StartRunAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.AgentRunEvents(runId) + query);
        if (lastEventId is not null)
        {
            request.Headers.Add("Last-Event-ID", lastEventId);
        }

        using var response = await Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Events_StreamLiveEventsWithKeepAlivesUntilTheRunEnds()
    {
        var runId = await StartRunAsync();
        await PublishAsync(runId, AgentEventTypes.TextDelta, new TextDeltaEventDto { Text = "a" });
        using var response = await Client.GetAsync(ApiRoutes.AgentRunEvents(runId), HttpCompletionOption.ResponseHeadersRead, Ct);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(Ct));
        var stream = new SseReader(reader);

        (await stream.NextEventAsync()).Id.ShouldBe("1");
        await PublishAsync(runId, AgentEventTypes.TextDelta, new TextDeltaEventDto { Text = "b" });
        (await stream.NextEventAsync()).Data.ShouldBe("""{"text":"b"}""");
        var keepAlive = stream.NextCommentAsync();
        await AdvanceUntilAsync(keepAlive);
        (await keepAlive).ShouldBe(" keep-alive");
        await PublishAsync(runId, AgentEventTypes.RunCancelled, new RunCancelledEventDto { RunId = runId });
        (await stream.NextEventAsync()).Event.ShouldBe(AgentEventTypes.RunCancelled);
        (await stream.IsEndAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task Tools_ListTheRegistryViewForTheContext()
    {
        var trackId = Guid.NewGuid();
        StatusService.ListTools(Arg.Any<PageContextDto>()).Returns(
        [
            new AgentToolInfoDto { Name = "mute", Feature = "001", Kind = ToolKindDto.Ui, Risk = ToolRiskDto.Change, Undo = UndoKindDto.Inverse, Available = false, UnavailableReason = "Open the mixer", Title = "Mute", Description = "Mutes a stem" },
        ]);

        using var response = await Client.GetAsync($"{ApiRoutes.AgentTools}?page=mixer&trackId={trackId}", Ct);
        using var noPage = await Client.GetAsync(ApiRoutes.AgentTools, Ct);
        using var bad = await Client.GetAsync($"{ApiRoutes.AgentTools}?trackId=nope", Ct);

        await ContractAssert.MatchesAsync(response, "listAgentTools");
        (await response.BodyAsync<AgentToolInfoDto[]>()).Single().Name.ShouldBe("mute");
        StatusService.Received().ListTools(Arg.Is<PageContextDto>(c => c.Page == "mixer" && c.TrackId == trackId));
        StatusService.Received().ListTools(Arg.Is<PageContextDto>(c => c.Page == AgentEndpointDefaults.Page && c.TrackId == null));
        bad.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(bad, "listAgentTools");
        noPage.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UiResults_AreDeliveredOnceOr409()
    {
        var runId = await StartRunAsync();
        var stepId = Guid.NewGuid();
        UiGateway.TryComplete(runId, stepId, Arg.Any<UiToolResult>()).Returns(true, false);
        var body = new { ok = true, result = new { gainDb = -3 } };

        using var first = await Client.PostAsJsonAsync(ApiRoutes.AgentUiResult(runId, stepId), body, Ct);
        using var second = await Client.PostAsJsonAsync(ApiRoutes.AgentUiResult(runId, stepId), body, Ct);
        using var unknown = await Client.PostAsJsonAsync(ApiRoutes.AgentUiResult(Guid.NewGuid(), stepId), body, Ct);
        using var tooLong = await Client.PostAsJsonAsync(ApiRoutes.AgentUiResult(runId, stepId), new { ok = false, error = new string('e', 501) }, Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ContractAssert.MatchesAsync(first, "submitAgentUiResult");
        UiGateway.Received().TryComplete(runId, stepId, Arg.Is<UiToolResult>(r => r.Ok && r.Result == """{"gainDb":-3}""" && r.Error == null));
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.JsonAsync()).GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.UiResultNotExpected));
        await ContractAssert.MatchesAsync(second, "submitAgentUiResult");
        await ContractAssert.MatchesAsync(unknown, "submitAgentUiResult");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        tooLong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_Returns202WithTheRunOr409WhenEnded()
    {
        var runId = await StartRunAsync();
        RunService.CancelAsync(runId, true, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask, Task.FromException(new AgentConflictException(AgentProblemTypes.RunEnded, "The run has ended.")));

        using var first = await Client.PostAsJsonAsync(ApiRoutes.AgentCancel(runId), new CancelAgentRunRequest { CancelJobs = true }, Ct);
        using var second = await Client.PostAsJsonAsync(ApiRoutes.AgentCancel(runId), new CancelAgentRunRequest { CancelJobs = true }, Ct);
        using var unknown = await Client.PostAsJsonAsync(ApiRoutes.AgentCancel(Guid.NewGuid()), new CancelAgentRunRequest(), Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(first, "cancelAgentRun");
        (await first.BodyAsync<AgentRunDto>()).RunId.ShouldBe(runId);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.JsonAsync()).GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.RunEnded));
        await ContractAssert.MatchesAsync(second, "cancelAgentRun");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(unknown, "cancelAgentRun");
    }

    [Fact]
    public async Task Cancel_ARunUnknownToTheAgent_Is404()
    {
        var runId = await StartRunAsync();
        RunService.CancelAsync(runId, false, Arg.Any<CancellationToken>()).ThrowsAsync(new KeyNotFoundException("gone"));

        using var response = await Client.PostAsJsonAsync(ApiRoutes.AgentCancel(runId), new CancelAgentRunRequest(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "cancelAgentRun");
    }

    private async Task<Guid> StartRunAsync()
    {
        var conversation = await CreateConversationAsync();
        using var response = await SendMessageAsync(conversation.ConversationId);
        return (await response.BodyAsync<AgentMessageAcceptedDto>()).RunId;
    }

    private async Task<ConversationDto> GetConversationAsync(Guid conversationId)
    {
        using var response = await Client.GetAsync(ApiRoutes.AgentConversation(conversationId), Ct);
        return await response.BodyAsync<ConversationDto>();
    }

    private Task<RunEvent> PublishAsync<T>(Guid runId, string type, T payload) =>
        Service<IRunEventSink>().PublishAsync(runId, type, JsonSerializer.Serialize(payload, ApiFactory.Json), Ct);

    private async Task<IReadOnlyList<SseEvent>> ReadStreamAsync(Guid runId, int? after, string? lastEventId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.AgentRunEvents(runId, after));
        if (lastEventId is not null)
        {
            request.Headers.Add("Last-Event-ID", lastEventId);
        }

        using var response = await Client.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        return SseReader.Parse(await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task AdvanceUntilAsync(Task done)
    {
        for (var attempt = 0; attempt < 200 && !done.IsCompleted; attempt++)
        {
            Factory.Clock.Advance(TimeSpan.FromSeconds(AgentContractLimits.KeepAliveSeconds));
            await Task.WhenAny(done, Task.Delay(25, Ct));
        }
    }
}
