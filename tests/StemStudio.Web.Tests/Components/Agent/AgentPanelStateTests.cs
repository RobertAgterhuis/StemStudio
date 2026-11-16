using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Agent;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Components.Agent;

/// <summary>Panel state edge cases: close, cancel without a run, lost streams, load errors and the badge labels.</summary>
public sealed class AgentPanelStateTests : PageTestContext
{
    [Fact]
    public void ToggleAndClose_ChangeIsOpen_AndNotify()
    {
        var changes = 0;
        Panel.Changed += () => changes++;

        Panel.Toggle();
        var open = Panel.IsOpen;
        Panel.Close();

        (open, Panel.IsOpen, changes).ShouldBe((true, false, 2));
    }

    [Fact]
    public async Task NewConversation_DuringTheFirstLoad_IsNotReplacedByTheNewestStoredOne()
    {
        var list = new TaskCompletionSource<IReadOnlyList<ConversationSummaryDto>>();
        AgentApi.ListConversationsAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns(list.Task);
        StoredOther();

        var load = Panel.LoadAsync();
        await Panel.NewConversationAsync();
        list.SetResult([AgentData.Summary(AgentData.OtherConversationId)]);
        await load;

        (Panel.ConversationId, Panel.Messages.Count).ShouldBe((AgentData.ConversationId, 0));
    }

    [Fact]
    public async Task NewConversation_StillBeingCreated_WhenTheFirstLoadEnds_Wins()
    {
        var created = new TaskCompletionSource<ConversationDto>();
        AgentApi.CreateConversationAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(created.Task);
        AgentApi.ListConversationsAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([AgentData.Summary(AgentData.OtherConversationId)]);
        StoredOther();

        var creating = Panel.NewConversationAsync();
        await Panel.LoadAsync();
        created.SetResult(AgentData.Conversation());
        await creating;

        (Panel.ConversationId, Panel.Messages.Count).ShouldBe((AgentData.ConversationId, 0));
    }

    [Fact]
    public async Task NewConversation_WhileTheNewestStoredOneIsStillLoading_Wins()
    {
        var stored = new TaskCompletionSource<ConversationDto>();
        AgentApi.ListConversationsAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([AgentData.Summary(AgentData.OtherConversationId)]);
        AgentApi.GetConversationAsync(AgentData.OtherConversationId, Arg.Any<CancellationToken>()).Returns(stored.Task);

        var load = Panel.LoadAsync();
        await Panel.NewConversationAsync();
        stored.SetResult(AgentData.Conversation(AgentData.OtherConversationId, messages: [AgentData.Message(1, MessageRoleDto.User, "Verwijder deze track")]));
        await load;

        (Panel.ConversationId, Panel.Messages.Count).ShouldBe((AgentData.ConversationId, 0));
    }

    [Fact]
    public async Task TheNewestStoredOne_LoadedAfterANewConversationWasCreated_IsNotShown()
    {
        var created = new TaskCompletionSource<ConversationDto>();
        var stored = new TaskCompletionSource<ConversationDto>();
        AgentApi.CreateConversationAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(created.Task);
        AgentApi.ListConversationsAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([AgentData.Summary(AgentData.OtherConversationId)]);
        AgentApi.GetConversationAsync(AgentData.OtherConversationId, Arg.Any<CancellationToken>()).Returns(stored.Task);

        var creating = Panel.NewConversationAsync();
        var load = Panel.LoadAsync();
        created.SetResult(AgentData.Conversation());
        await creating;
        stored.SetResult(AgentData.Conversation(AgentData.OtherConversationId, messages: [AgentData.Message(1, MessageRoleDto.User, "Verwijder deze track")]));
        await load;

        (Panel.ConversationId, Panel.Messages.Count).ShouldBe((AgentData.ConversationId, 0));
    }

    private void StoredOther() =>
        AgentApi.GetConversationAsync(AgentData.OtherConversationId, Arg.Any<CancellationToken>())
            .Returns(AgentData.Conversation(AgentData.OtherConversationId, messages: [AgentData.Message(1, MessageRoleDto.User, "Verwijder deze track")]));

    [Fact]
    public async Task Cancel_WithoutARun_CallsNothing()
    {
        await Panel.CancelAsync();

        await AgentApi.DidNotReceive().CancelRunAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_WhenNotReady_IsRefused()
    {
        (await Panel.SendAsync("Zoek iets")).ShouldBeFalse();

        await AgentApi.DidNotReceive().SendMessageAsync(Arg.Any<Guid>(), Arg.Any<SendAgentMessageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Load_Unreachable_ShowsTheError()
    {
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns<AgentStatusDto>(_ => throw new ApiException(ApiError.Unreachable));

        await Panel.LoadAsync();

        (Panel.Error, Panel.IsReady, Panel.Backend).ShouldBe((ApiError.Unreachable.Title, false, null));
    }

    [Fact]
    public async Task LostStream_WithARunningSnapshot_RestoresItAndSaysTheConnectionWasLost()
    {
        AgentApi.GetRunAsync(AgentData.RunId, Arg.Any<CancellationToken>()).Returns(AgentData.Run() with { Steps = [AgentData.Step()] });
        await StartRunAsync();

        AgentEvents.Fail(new ApiException(ApiError.Unreachable));
        await Panel.Following;

        Panel.Error.ShouldNotBeNull().ShouldContain("connection to the agent run was lost");
        Panel.Run!.Steps.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task LostStream_WithoutASnapshot_KeepsTheViewAndSaysSo()
    {
        AgentApi.GetRunAsync(AgentData.RunId, Arg.Any<CancellationToken>()).Returns<AgentRunDto>(_ => throw new ApiException(ApiError.Unreachable));
        await StartRunAsync();
        AgentEvents.Text("Working");

        AgentEvents.Fail(new ApiException(ApiError.Unreachable));
        await Panel.Following;

        (Panel.Run!.Text, Panel.Error is not null).ShouldBe(("Working", true));
    }

    [Fact]
    public async Task SwitchingConversation_StopsFollowingTheRun()
    {
        await StartRunAsync();

        await Panel.SelectConversationAsync(AgentData.OtherConversationId);
        await Panel.Following;

        (Panel.Run, Panel.ConversationId).ShouldBe((null, AgentData.OtherConversationId));
    }

    [Fact]
    public async Task UsingFallback_ComesFromTheStatus_BeforeARunStarts()
    {
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Status() with { UsingFallback = true, ActiveModel = "qwen3.5:4b" });

        await Panel.LoadAsync();

        (Panel.UsingFallback, Panel.Model, Panel.Backend).ShouldBe((true, "qwen3.5:4b", AgentBackendDto.Ollama));
    }

    [Theory]
    [InlineData(AgentBackendDto.Claude, false, "Claude claude-opus-5-5", "claude")]
    [InlineData(AgentBackendDto.Ollama, true, "Local claude-opus-5-5 (fallback)", "ollama")]
    [InlineData(AgentBackendDto.Fake, false, "Test backend", "fake")]
    [InlineData(null, false, "Agent off", "none")]
    public void Badge_LabelsEveryBackend(AgentBackendDto? backend, bool fallback, string label, string wire)
    {
        var cut = Render<BackendBadge>(p => p.Add(b => b.Backend, backend).Add(b => b.Model, "claude-opus-5-5").Add(b => b.Fallback, fallback));

        cut.Find("[data-testid=agent-backend-badge]").TextContent.ShouldContain(label);
        cut.Find("[data-testid=agent-backend-badge]").GetAttribute("data-backend").ShouldBe(wire);
    }

    [Fact]
    public void Plan_ShowsAnIconPerStatus()
    {
        PlanItemDto Item(int index, PlanItemStatusDto status) => new() { Index = index, Title = $"Item {index}", Status = status };

        var cut = Render<PlanView>(p => p.Add(v => v.Plan, [Item(0, PlanItemStatusDto.Failed), Item(1, PlanItemStatusDto.Skipped)]));

        cut.Find("[data-testid=agent-plan-item-0] .rzi").TextContent.ShouldBe("error");
        cut.Find("[data-testid=agent-plan-item-1] .rzi").TextContent.ShouldBe("block");
    }

    [Fact]
    public async Task AnEndedRun_IsAnnouncedToThePages()
    {
        var ended = 0;
        Services.GetRequiredService<StemStudio.Web.Agent.AgentRunNotifier>().RunEnded += () => ended++;
        await StartRunAsync();

        AgentEvents.Started().Completed();
        AgentEvents.End();
        await Panel.Following;

        ended.ShouldBe(1);
    }

    private async Task StartRunAsync()
    {
        await Panel.LoadAsync();
        (await Panel.SendAsync("Zoek iets")).ShouldBeTrue();
    }
}
