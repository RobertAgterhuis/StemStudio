using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Tests.Agent;

/// <summary>011 T070: undo, rename and delete of conversations, history after a reload (FR-002, FR-017).</summary>
public sealed class HistoryAndUndoTests : AgentApiTestBase
{
    [Fact]
    public async Task Undo_QueuesTheUndoRunOfTheAgent()
    {
        var conversation = await CreateConversationAsync();
        var undoRun = AgentRecords.Run(conversation.ConversationId);
        RunService.UndoAsync(conversation.ConversationId, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await Service<IRunStore>().CreateRunAsync(undoRun, Ct);
            return undoRun.Id;
        });

        using var response = await Client.PostAsync(ApiRoutes.AgentUndo(conversation.ConversationId), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "undoLastAgentAction");
        (await response.BodyAsync<AgentRunAcceptedDto>()).RunId.ShouldBe(undoRun.Id);
        await Eventually.WaitUntilAsync(() => Task.FromResult(RunService.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IAgentRunService.ExecuteAsync))));
        await RunService.Received(1).ExecuteAsync(undoRun.Id, Arg.Any<CancellationToken>());
        (await Service<IConversationStore>().GetAsync(conversation.ConversationId, Ct))!.ActiveRunId.ShouldBe(undoRun.Id);
    }

    [Theory]
    [InlineData(AgentProblemTypes.NothingToUndo)]
    [InlineData(AgentProblemTypes.RunActive)]
    public async Task Undo_MapsAgentConflictsTo409(string code)
    {
        var conversation = await CreateConversationAsync();
        RunService.UndoAsync(conversation.ConversationId, Arg.Any<CancellationToken>()).ThrowsAsync(new AgentConflictException(code, "Nothing can be undone."));

        using var response = await Client.PostAsync(ApiRoutes.AgentUndo(conversation.ConversationId), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "undoLastAgentAction");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(code));
    }

    [Fact]
    public async Task Undo_WhileARunIsActive_Is409WithoutAskingTheAgent()
    {
        var conversation = await CreateConversationAsync();
        using var sent = await SendMessageAsync(conversation.ConversationId);
        sent.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var runs = Service<IRunStore>();
        var runId = (await sent.BodyAsync<AgentMessageAcceptedDto>()).RunId;
        await runs.UpdateRunAsync((await runs.GetRunAsync(runId, Ct))! with { Status = AgentRunStatusDto.AwaitingApproval }, Ct);

        using var response = await Client.PostAsync(ApiRoutes.AgentUndo(conversation.ConversationId), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.RunActive));
        await RunService.DidNotReceive().UndoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Undo_ConversationUnknownToTheAgent_Is404()
    {
        var conversation = await CreateConversationAsync();
        RunService.UndoAsync(conversation.ConversationId, Arg.Any<CancellationToken>()).ThrowsAsync(new KeyNotFoundException("gone"));

        using var response = await Client.PostAsync(ApiRoutes.AgentUndo(conversation.ConversationId), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "undoLastAgentAction");
    }

    [Fact]
    public async Task Undo_UnknownConversation_Is404()
    {
        using var response = await Client.PostAsync(ApiRoutes.AgentUndo(Guid.NewGuid()), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "undoLastAgentAction");
    }

    [Fact]
    public async Task Rename_ChangesTheTitle()
    {
        var conversation = await CreateConversationAsync("Old");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));

        using var response = await Client.PatchAsJsonAsync(ApiRoutes.AgentConversation(conversation.ConversationId), new RenameConversationRequest { Title = "  Drums  " }, Ct);

        await ContractAssert.MatchesAsync(response, "renameConversation");
        var summary = await response.BodyAsync<ConversationSummaryDto>();
        (summary.Title, summary.UpdatedAt).ShouldBe(("Drums", ApiFactory.Start.AddMinutes(1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("long")]
    public async Task Rename_RejectsEmptyOrLongTitlesWith422(string title)
    {
        var conversation = await CreateConversationAsync();

        using var response = await Client.PatchAsJsonAsync(ApiRoutes.AgentConversation(conversation.ConversationId),
            new RenameConversationRequest { Title = title == "long" ? new string('x', 121) : title }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "renameConversation");
    }

    [Fact]
    public async Task RenameAndDelete_UnknownConversation_Are404()
    {
        using var rename = await Client.PatchAsJsonAsync(ApiRoutes.AgentConversation(Guid.NewGuid()), new RenameConversationRequest { Title = "x" }, Ct);
        using var delete = await Client.DeleteAsync(ApiRoutes.AgentConversation(Guid.NewGuid()), Ct);

        await ContractAssert.MatchesAsync(rename, "renameConversation");
        await ContractAssert.MatchesAsync(delete, "deleteConversation");
        (rename.StatusCode, delete.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Delete_IsRefusedWhileARunIsActiveThenRemovesEverything()
    {
        var conversation = await CreateConversationAsync();
        using var sent = await SendMessageAsync(conversation.ConversationId);
        var runId = (await sent.BodyAsync<AgentMessageAcceptedDto>()).RunId;

        using var refused = await Client.DeleteAsync(ApiRoutes.AgentConversation(conversation.ConversationId), Ct);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(refused, "deleteConversation");
        (await refused.JsonAsync()).GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.RunActive));

        var runs = Service<IRunStore>();
        await runs.UpdateRunAsync((await runs.GetRunAsync(runId, Ct))! with { Status = AgentRunStatusDto.Completed }, Ct);
        using var deleted = await Client.DeleteAsync(ApiRoutes.AgentConversation(conversation.ConversationId), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ContractAssert.MatchesAsync(deleted, "deleteConversation");
        (await runs.GetRunAsync(runId, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Reload_RestoresMessagesPlansAndSteps()
    {
        var conversation = await CreateConversationAsync();
        using var sent = await SendMessageAsync(conversation.ConversationId);
        var runId = (await sent.BodyAsync<AgentMessageAcceptedDto>()).RunId;
        var runs = Service<IRunStore>();
        var plan = new List<PlanItem> { new() { Index = 0, Title = "Mute drums", Tool = "mixer_set_channel", Status = PlanItemStatusDto.Done } };
        await runs.UpdateRunAsync((await runs.GetRunAsync(runId, Ct))! with { Status = AgentRunStatusDto.Completed, Plan = plan, EndedAt = ApiFactory.Start }, Ct);
        await runs.SaveStepAsync(AgentRecords.Step(runId, 1, AgentStepStatusDto.Succeeded), Ct);
        await Service<IConversationStore>().AppendMessageAsync(AgentRecords.Message(conversation.ConversationId, 2, MessageRoleDto.Assistant, runId), Ct);

        using var reloaded = new ApiFactory(Factory.DataDir);
        using var client = reloaded.CreateClient();
        var history = await (await client.GetAsync(ApiRoutes.AgentConversation(conversation.ConversationId), Ct)).BodyAsync<ConversationDto>();
        var run = await (await client.GetAsync(ApiRoutes.AgentRun(runId), Ct)).BodyAsync<AgentRunDto>();

        history.Messages.Select(m => m.Role).ShouldBe([MessageRoleDto.User, MessageRoleDto.Assistant]);
        (history.LastRunStatus, history.Runs.Single().RunId).ShouldBe((AgentRunStatusDto.Completed, runId));
        (run.Plan.Single().Title, run.Plan.Single().Status, run.Steps.Single().Status).ShouldBe(("Mute drums", PlanItemStatusDto.Done, AgentStepStatusDto.Succeeded));
    }
}
