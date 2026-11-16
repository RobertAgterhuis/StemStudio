using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Agent.Backends.Fakes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Mixes;
using static StemStudio.Agent.Tests.Fakes.TestTools;

namespace StemStudio.Agent.Tests.Runtime;

/// <summary>
/// T068: undo entries are recorded for executed change/job steps with the catalogue's inverse or compensation; Undo applies
/// the newest available group in reverse order as a new run without a model turn; fresh compensations run without approval,
/// others need it; entries become obsolete when the target changed outside the agent or vanished (FR-017, SC-007).
/// </summary>
public sealed class UndoPlannerTests
{
    private static readonly Guid TrackId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid MixId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, (double GainDb, double Pan, bool Mute, bool Solo)> _channels = new(StringComparer.Ordinal)
    {
        ["vocals"] = (0, 0, false, false),
        ["drums"] = (-3, 0.2, false, false),
        ["bass"] = (-6, 0, true, false),
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ScriptedCall SetChannel(string stem, double gainDb) => Call("mixer_set_channel", $$"""{"stem":"{{stem}}","gainDb":{{gainDb}}}""");

    private static (double GainDb, double Pan, bool Mute, bool Solo) Applied((double GainDb, double Pan, bool Mute, bool Solo) previous, JsonElement root) => previous with
    {
        GainDb = root.TryGetProperty("gainDb", out var gain) ? gain.GetDouble() : previous.GainDb,
        Pan = root.TryGetProperty("pan", out var pan) ? pan.GetDouble() : previous.Pan,
        Mute = root.TryGetProperty("mute", out var mute) ? mute.GetBoolean() : previous.Mute,
        Solo = root.TryGetProperty("solo", out var solo) ? solo.GetBoolean() : previous.Solo,
    };

    /// <summary>A mixer page that applies mixer_set_channel and answers {stem, previous, current} like the frontend.</summary>
    private UiToolResult Mixer(UiToolRequest request)
    {
        if (request.Tool == "mixer_get_state")
        {
            return new UiToolResult { Ok = true, Result = JsonSerializer.Serialize(new { trackId = TrackId, settings = Dtos.Settings() }, Web) };
        }

        using var arguments = JsonDocument.Parse(request.Arguments);
        var root = arguments.RootElement;
        var stem = root.GetProperty("stem").GetString()!;
        var previous = _channels[stem];
        var current = Applied(previous, root);
        _channels[stem] = current;
        return new UiToolResult { Ok = true, Result = JsonSerializer.Serialize(new { stem, previous = View(stem, previous), current = View(stem, current) }, Web) };
    }

    private static object View(string stem, (double GainDb, double Pan, bool Mute, bool Solo) channel) =>
        new { stem, gainDb = channel.GainDb, pan = channel.Pan, mute = channel.Mute, solo = channel.Solo };

    private RunnerHarness MixerHarness(params ScriptedTurn[] turns) => PageHarness("mixer", turns);

    private RunnerHarness PageHarness(string page, params ScriptedTurn[] turns)
    {
        var harness = new RunnerHarness(new ScriptedChatClient(turns), [], "Drums -6, vocals +2, bass -10", page)
        {
            Registry = new AgentToolRegistry([new Core001ToolProvider(), new MetaToolProvider()]),
        };
        harness.Ui.Responder = Mixer;
        return harness;
    }

    private static async Task<AgentRun> UndoAsync(RunnerHarness harness, AgentRunService service)
    {
        var runId = await service.UndoAsync(harness.ConversationId, Token);
        await service.ExecuteAsync(runId, Token);
        return (await harness.Runs.GetRunAsync(runId, Token))!;
    }

    [Fact]
    public async Task ExecutedChangeSteps_RecordInverseEntries_OneGroupPerRun()
    {
        var harness = MixerHarness(Turn(SetChannel("drums", -6), SetChannel("vocals", 2), SetChannel("bass", -10)), Say("Done."));

        await harness.ExecuteAsync();

        var entries = harness.Undo.Entries;
        entries.Count.ShouldBe(3);
        entries.ShouldAllBe(entry => entry.RunId == harness.Run.Id && entry.ConversationId == harness.ConversationId);
        entries.ShouldAllBe(entry => entry.Kind == UndoKindDto.Inverse && !entry.Fresh && entry.Status == UndoEntryStatus.Available);
        entries[0].Action.Tool.ShouldBe("mixer_set_channel");
        JsonNode.Parse(entries[0].Action.Arguments)!.ToJsonString().ShouldBe("""{"gainDb":-3,"mute":false,"pan":0.2,"solo":false,"stem":"drums"}""");
        entries.Select(entry => entry.StepId).ShouldBe(harness.Runs.Steps.OrderBy(step => step.Seq).Select(step => step.Id));
    }

    [Fact]
    public async Task ReadAndFailedSteps_RecordNothing()
    {
        var harness = MixerHarness(Turn(Call("mixer_get_state"), SetChannel("unknown-stem", 1)), Say("ok"));
        harness.Ui.Responder = request => request.Tool == "mixer_get_state"
            ? new UiToolResult { Ok = true, Result = "{}" }
            : new UiToolResult { Ok = false, Error = "This track has no channel 'unknown-stem'." };

        await harness.ExecuteAsync();

        harness.Undo.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Undo_RestoresTheNewestGroupInReverseOrder_WithoutAModelTurn()
    {
        var harness = MixerHarness(Turn(SetChannel("drums", -6), SetChannel("vocals", 2), SetChannel("bass", -10)), Say("Done."));
        await harness.ExecuteAsync();
        var service = new AgentRunService(harness.Services());
        var requestsBefore = harness.Script.Requests.Count;
        var uiBefore = harness.Ui.Requests.Count;

        var undo = await UndoAsync(harness, service);

        undo.Status.ShouldBe(AgentRunStatusDto.Completed);
        (undo.Model, undo.ModelTurns).ShouldBe((UndoPlanner.UndoModel, 0));
        harness.Script.Requests.Count.ShouldBe(requestsBefore);
        harness.Ui.Requests.Skip(uiBefore).Select(request => JsonDocument.Parse(request.Arguments).RootElement.GetProperty("stem").GetString()).ShouldBe(["bass", "vocals", "drums"]);
        _channels["drums"].ShouldBe((-3, 0.2, false, false));
        _channels["vocals"].ShouldBe((0, 0, false, false));
        _channels["bass"].ShouldBe((-6, 0, true, false));
        harness.Undo.Entries.ShouldAllBe(entry => entry.Status == UndoEntryStatus.Applied);
        harness.Runs.Steps.Where(step => step.RunId == harness.Run.Id).ShouldAllBe(step => step.Status == AgentStepStatusDto.Undone);
        harness.Runs.Steps.Where(step => step.RunId == undo.Id).Count().ShouldBe(3);
    }

    [Fact]
    public async Task Undo_IsAUserMessagePlusAnAnswer_AndTheUndoRunRecordsNoEntries()
    {
        var harness = MixerHarness(Turn(SetChannel("drums", -6)), Say("Done."));
        await harness.ExecuteAsync();
        var service = new AgentRunService(harness.Services());

        var undo = await UndoAsync(harness, service);

        var messages = await harness.Conversations.GetMessagesAsync(harness.ConversationId, Token);
        var command = messages.Single(message => message.Id == undo.UserMessageId);
        (command.Role, command.Text, command.Context.Page, command.RunId).ShouldBe((MessageRoleDto.User, UndoPlanner.UndoText, "mixer", undo.Id));
        messages[^1].Role.ShouldBe(MessageRoleDto.Assistant);
        messages[^1].Text.ShouldBe("Undone: 1 step.");
        harness.Undo.Entries.Count.ShouldBe(1);
        harness.Events.Types.ShouldContain(AgentEventTypes.RunCompleted);
    }

    [Fact]
    public async Task UndoAsync_StoresTheQueuedRun_BeforeItReturnsItsId()
    {
        var harness = MixerHarness(Turn(SetChannel("drums", -6)), Say("Done."));
        await harness.ExecuteAsync();

        var runId = await new AgentRunService(harness.Services()).UndoAsync(harness.ConversationId, Token);

        var queued = await harness.Runs.GetRunAsync(runId, Token);
        queued.ShouldNotBeNull();
        (queued.Status, queued.ConversationId, queued.Model).ShouldBe((AgentRunStatusDto.Queued, harness.ConversationId, UndoPlanner.UndoModel));
    }

    [Fact]
    public async Task SecondUndo_TakesTheOlderGroup_ThenNothingIsLeft()
    {
        var harness = MixerHarness(Turn(SetChannel("drums", -6)), Say("Done."));
        await harness.ExecuteAsync();
        var service = new AgentRunService(harness.Services());
        await SecondCommandAsync(harness, SetChannel("drums", -12));

        await UndoAsync(harness, service);
        _channels["drums"].GainDb.ShouldBe(-6);
        await UndoAsync(harness, service);
        _channels["drums"].GainDb.ShouldBe(-3);

        var nothing = await Should.ThrowAsync<AgentConflictException>(() => service.UndoAsync(harness.ConversationId, Token));
        nothing.ProblemCode.ShouldBe(AgentProblemTypes.NothingToUndo);
    }

    [Fact]
    public async Task Undo_UnknownConversation_AndActiveRun_AreRefused()
    {
        var harness = MixerHarness(Say("ok"));
        await harness.SeedAsync();
        var service = new AgentRunService(harness.Services());

        await Should.ThrowAsync<KeyNotFoundException>(() => service.UndoAsync(Guid.NewGuid(), Token));
        var active = await Should.ThrowAsync<AgentConflictException>(() => service.UndoAsync(harness.ConversationId, Token));

        active.ProblemCode.ShouldBe(AgentProblemTypes.RunActive);
    }

    [Fact]
    public async Task FreshCompensation_DeletesTheSavedMix_WithoutApproval()
    {
        var harness = MixerHarness(Turn(Call("save_mix_as", $$"""{"trackId":"{{TrackId}}","name":"Agent mix","fromOpenMixer":true}""")), Say("Saved."));
        harness.Actions.Mixes.CreateMixAsync(Arg.Any<CreateMixRequest>(), Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "Agent mix"));
        harness.Actions.Mixes.GetMixAsync(MixId, Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "Agent mix"));
        await harness.ExecuteAsync();
        var entry = harness.Undo.Entries.Single();
        (entry.Kind, entry.Fresh, entry.Action.Tool).ShouldBe((UndoKindDto.Compensate, true, "delete_mix"));

        var undo = await UndoAsync(harness, new AgentRunService(harness.Services()));

        undo.Status.ShouldBe(AgentRunStatusDto.Completed);
        await harness.Actions.Mixes.Received(1).DeleteMixAsync(MixId, Arg.Any<CancellationToken>());
        harness.Events.Types.ShouldNotContain(AgentEventTypes.ApprovalRequested);
    }

    [Fact]
    public async Task FreshCompensation_OfAMixChangedOutsideTheAgent_IsObsolete()
    {
        var harness = MixerHarness(Turn(Call("save_mix_as", $$"""{"trackId":"{{TrackId}}","name":"Agent mix","fromOpenMixer":true}""")), Say("Saved."));
        harness.Actions.Mixes.CreateMixAsync(Arg.Any<CreateMixRequest>(), Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "Agent mix"));
        await harness.ExecuteAsync();
        harness.Actions.Mixes.GetMixAsync(MixId, Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "Agent mix") with { UpdatedAt = Dtos.At.AddMinutes(5) });

        var undo = await UndoAsync(harness, new AgentRunService(harness.Services()));

        await harness.Actions.Mixes.DidNotReceive().DeleteMixAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        harness.Undo.Entries.Single().Status.ShouldBe(UndoEntryStatus.Obsolete);
        var answer = (await harness.Conversations.GetMessagesAsync(harness.ConversationId, Token))[^1].Text;
        answer.ShouldBe($"Undone: 0 steps. Not undone: delete_mix ({UndoPlanner.ChangedOutside})");
        undo.Status.ShouldBe(AgentRunStatusDto.Completed);
    }

    [Fact]
    public async Task InverseOfAVanishedMix_IsObsolete()
    {
        var harness = PageHarness("tracks", Turn(Call("rename_mix", $$"""{"mixId":"{{MixId}}","name":"New"}""")), Say("Renamed."));
        harness.Actions.Mixes.GetMixAsync(MixId, Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "Old"));
        harness.Actions.Mixes.UpdateMixAsync(MixId, Arg.Any<MixSettingsDto>(), Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "New"));
        await harness.ExecuteAsync();
        harness.Actions.Mixes.GetMixAsync(MixId, Arg.Any<CancellationToken>()).ThrowsAsync(new AgentActionException(404, null, "The mix does not exist."));
        harness.Actions.Mixes.ClearReceivedCalls();

        await UndoAsync(harness, new AgentRunService(harness.Services()));

        await harness.Actions.Mixes.DidNotReceive().UpdateMixAsync(Arg.Any<Guid>(), Arg.Any<MixSettingsDto>(), Arg.Any<CancellationToken>());
        harness.Undo.Entries.Single().Status.ShouldBe(UndoEntryStatus.Obsolete);
        (await harness.Conversations.GetMessagesAsync(harness.ConversationId, Token))[^1].Text.ShouldContain(UndoPlanner.Vanished);
    }

    [Fact]
    public async Task InverseOfAnUnchangedMix_RestoresThePreviousName()
    {
        var harness = PageHarness("tracks", Turn(Call("rename_mix", $$"""{"mixId":"{{MixId}}","name":"New"}""")), Say("Renamed."));
        harness.Actions.Mixes.GetMixAsync(MixId, Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "Old"));
        harness.Actions.Mixes.UpdateMixAsync(MixId, Arg.Any<MixSettingsDto>(), Arg.Any<CancellationToken>()).Returns(Dtos.Mix(MixId, TrackId, "New"));
        await harness.ExecuteAsync();
        harness.Actions.Mixes.ClearReceivedCalls();

        await UndoAsync(harness, new AgentRunService(harness.Services()));

        await harness.Actions.Mixes.Received(1).UpdateMixAsync(MixId, Arg.Is<MixSettingsDto>(settings => settings.Name == "Old"), Arg.Any<CancellationToken>());
        harness.Undo.Entries.Single().Status.ShouldBe(UndoEntryStatus.Applied);
    }

    [Fact]
    public async Task NonFreshCompensation_NeedsApproval_AndARejectionLeavesItObsolete()
    {
        var harness = RunnerHarness.Scripted([Created(), DeleteThing()], Turn(Call("create_thing")), Say("ok"));
        await harness.ExecuteAsync();
        var service = new AgentRunService(harness.Services());
        var runId = await service.UndoAsync(harness.ConversationId, Token);
        var execution = service.ExecuteAsync(runId, Token);

        var requested = await harness.Events.WaitForAsync(AgentEventTypes.ApprovalRequested);
        var card = JsonDocument.Parse(requested.Payload).RootElement;
        card.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()).ShouldContain(AgentDecisionReasons.UndoCompensation);
        var step = harness.Runs.Steps.Single(item => item.RunId == runId);
        await service.DecideAsync(runId, step.Id, new DecideApprovalRequest { Decision = ApprovalDecisionDto.Reject, ArgumentsHash = step.ArgumentsHash }, Token);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Token);

        _deleted.ShouldBeEmpty();
        harness.Undo.Entries.Single().Status.ShouldBe(UndoEntryStatus.Obsolete);
    }

    [Fact]
    public async Task NothingToUndo_WhenOnlyReadsRan()
    {
        var harness = RunnerHarness.Scripted([Echo()], Turn(Call("echo")), Say("ok"));
        await harness.ExecuteAsync();

        var nothing = await Should.ThrowAsync<AgentConflictException>(() => new AgentRunService(harness.Services()).UndoAsync(harness.ConversationId, Token));

        nothing.ProblemCode.ShouldBe(AgentProblemTypes.NothingToUndo);
    }

    [Theory]
    [InlineData("mixer_set_master", """{"previous":-4.5,"current":-1}""", "mixer_set_master", """{"gainDb":-4.5}""")]
    [InlineData("set_loop", """{"previous":{"start":10,"end":20,"enabled":true},"current":{"start":1,"end":2,"enabled":true}}""", "set_loop", """{"enabled":true,"endSeconds":20,"startSeconds":10}""")]
    [InlineData("set_loop", """{"previous":null,"current":{"start":1,"end":2,"enabled":true}}""", "set_loop", """{"enabled":false}""")]
    [InlineData("set_metronome", """{"previous":{"enabled":false,"volume":0.8,"bpmOverride":null,"offsetMs":0,"beatsPerBar":4},"current":{}}""", "set_metronome", """{"beatsPerBar":4,"enabled":false,"offsetMs":0,"volume":0.8}""")]
    [InlineData("mixer_set_channel", """{"stem":"bass","previous":{"stem":"bass","gainDb":-90,"pan":0,"mute":false,"solo":false}}""", "mixer_set_channel", """{"gainDb":-60,"mute":false,"pan":0,"solo":false,"stem":"bass"}""")]
    public void UiInverses_FollowThePagesPreviousValues(string tool, string answer, string inverseTool, string inverseArguments)
    {
        var actions = UiUndo.From(tool, JsonDocument.Parse(answer).RootElement);

        actions.Select(action => (action.Tool, action.Arguments)).ShouldBe([(inverseTool, inverseArguments)]);
    }

    [Fact]
    public void UiInverses_OfAReset_SetEveryChannelBack_AndUnknownAnswersGiveNone()
    {
        var reset = UiUndo.From("mixer_reset", JsonDocument.Parse("""{"previous":[{"stem":"vocals","gainDb":-2,"pan":0,"mute":false,"solo":false},{"stem":"drums","gainDb":-6,"pan":0.5,"mute":true,"solo":false}]}""").RootElement);

        reset.Select(action => JsonDocument.Parse(action.Arguments).RootElement.GetProperty("stem").GetString()).ShouldBe(["vocals", "drums"]);
        UiUndo.From("transport", JsonDocument.Parse("{}").RootElement).ShouldBeEmpty();
        UiUndo.From("mixer_set_master", JsonDocument.Parse("""{"current":1}""").RootElement).ShouldBeEmpty();
    }

    private readonly List<string> _deleted = [];

    private static AgentToolDescriptor Created() => Server("create_thing", _ => new ToolResult
    {
        Projection = new JsonObject { ["id"] = "t1" },
        Undo = new UndoAction { Tool = "delete_thing", Arguments = """{"id":"t1"}""" },
    }, ToolRiskDto.Job) with
    { UndoKind = UndoKindDto.Compensate };

    private AgentToolDescriptor DeleteThing() => Server("delete_thing", invocation =>
    {
        _deleted.Add(invocation.Text("id")!);
        return ToolResult.Of(new JsonObject { ["deleted"] = true });
    }, ToolRiskDto.Destructive, ToolSchema.Of(Param.Text("id", 1, 10)));

    private static async Task SecondCommandAsync(RunnerHarness harness, ScriptedCall call)
    {
        var script = new ScriptedChatClient([Turn(call), Say("Done again.")]);
        var message = harness.Message(3, MessageRoleDto.User, "Drums -12", "mixer");
        await harness.Conversations.AppendMessageAsync(message, Token);
        var run = harness.Run with { Id = Guid.NewGuid(), UserMessageId = message.Id, Status = AgentRunStatusDto.Queued };
        await harness.Runs.CreateRunAsync(run, Token);
        harness.UseSession(new FixedChatSession(script));
        await new AgentRunService(harness.Services()).ExecuteAsync(run.Id, Token);
    }
}
