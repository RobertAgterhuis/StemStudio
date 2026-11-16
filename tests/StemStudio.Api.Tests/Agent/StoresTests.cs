using Microsoft.Extensions.DependencyInjection;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Stores;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Tests.Agent;

/// <summary>011 T020: the EF stores implement the agent ports; one active run; settings defaults; start-up recovery.</summary>
public sealed class StoresTests : ApiTestBase
{
    private IConversationStore Conversations => Factory.Services.GetRequiredService<IConversationStore>();

    private IRunStore Runs => Factory.Services.GetRequiredService<IRunStore>();

    private IUndoStore Undo => Factory.Services.GetRequiredService<IUndoStore>();

    private IAgentSettingsStore Settings => Factory.Services.GetRequiredService<IAgentSettingsStore>();

    [Fact]
    public async Task Conversations_RoundTripListUpdateAndDelete()
    {
        var older = AgentRecords.Conversation(AgentRecords.T0, "Older");
        var newer = AgentRecords.Conversation(AgentRecords.T0.AddHours(1), "Newer");
        await Conversations.CreateAsync(older, Ct);
        await Conversations.CreateAsync(newer, Ct);

        (await Conversations.GetAsync(older.Id, Ct)).ShouldBe(older);
        (await Conversations.GetAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
        (await Conversations.ListAsync(10, null, Ct)).Select(c => c.Title).ShouldBe(["Newer", "Older"]);
        (await Conversations.ListAsync(1, null, Ct)).Single().Title.ShouldBe("Newer");
        (await Conversations.ListAsync(10, AgentRecords.T0.AddMinutes(30), Ct)).Single().Title.ShouldBe("Older");

        var renamed = older with { Title = "Renamed", UpdatedAt = AgentRecords.T0.AddHours(2), ActiveRunId = Guid.NewGuid() };
        await Conversations.UpdateAsync(renamed, Ct);
        (await Conversations.GetAsync(older.Id, Ct)).ShouldBe(renamed);

        (await Conversations.DeleteAsync(older.Id, Ct)).ShouldBeTrue();
        (await Conversations.DeleteAsync(older.Id, Ct)).ShouldBeFalse();
        (await Conversations.GetAsync(older.Id, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task UpdatingAMissingConversation_Throws()
    {
        await Should.ThrowAsync<KeyNotFoundException>(() => Conversations.UpdateAsync(AgentRecords.Conversation(), Ct));
    }

    [Fact]
    public async Task Messages_AreReturnedInSeqOrderWithTheirContext()
    {
        var conversation = AgentRecords.Conversation();
        await Conversations.CreateAsync(conversation, Ct);
        var second = AgentRecords.Message(conversation.Id, 2, MessageRoleDto.Assistant, Guid.NewGuid());
        var first = AgentRecords.Message(conversation.Id, 1);
        await Conversations.AppendMessageAsync(second, Ct);
        await Conversations.AppendMessageAsync(first, Ct);

        (await Conversations.GetMessagesAsync(conversation.Id, Ct)).ShouldBe([first, second]);
        (await Conversations.GetMessagesAsync(Guid.NewGuid(), Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Runs_RoundTripAndOnlyOneIsActiveGlobally()
    {
        var conversation = AgentRecords.Conversation();
        await Conversations.CreateAsync(conversation, Ct);
        var run = AgentRecords.Run(conversation.Id);
        await Runs.CreateRunAsync(run, Ct);

        (await Runs.GetRunAsync(run.Id, Ct)).ShouldBeEquivalentTo(run);
        (await Runs.GetRunAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
        (await Runs.GetActiveRunAsync(Ct))!.Id.ShouldBe(run.Id);
        await Should.ThrowAsync<InvalidOperationException>(() => Runs.CreateRunAsync(AgentRecords.Run(conversation.Id), Ct));

        var ended = run with
        {
            Status = AgentRunStatusDto.Failed,
            Tainted = true,
            FailureCode = AgentFailureCodes.Timeout,
            FailureMessage = "The model did not answer in time.",
            EndedAt = AgentRecords.T0.AddMinutes(1),
            Usage = new TokenUsage(1, 2, 3),
        };
        await Runs.UpdateRunAsync(ended, Ct);
        (await Runs.GetRunAsync(run.Id, Ct)).ShouldBeEquivalentTo(ended);
        (await Runs.GetActiveRunAsync(Ct)).ShouldBeNull();
        await Runs.CreateRunAsync(AgentRecords.Run(conversation.Id), Ct);
    }

    [Fact]
    public async Task UpdatingAMissingRun_Throws()
    {
        await Should.ThrowAsync<KeyNotFoundException>(() => Runs.UpdateRunAsync(AgentRecords.Run(Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task StepsAndApprovals_AreInsertedAndReplaced()
    {
        var run = await CreateRunAsync();
        var second = AgentRecords.Step(run.Id, 2);
        var first = AgentRecords.Step(run.Id, 1) with { Job = null, PlanIndex = null, EstimatedMinutes = null, ResultSummary = null };
        await Runs.SaveStepAsync(second, Ct);
        await Runs.SaveStepAsync(first, Ct);
        var done = second with { Status = AgentStepStatusDto.Failed, Error = "Not found", EndedAt = AgentRecords.T0.AddSeconds(3), DecisionReasons = new List<string>() };
        await Runs.SaveStepAsync(done, Ct);

        var steps = await Runs.GetStepsAsync(run.Id, Ct);
        steps.Select(s => s.Seq).ShouldBe([1, 2]);
        steps[0].ShouldBeEquivalentTo(first);
        steps[1].ShouldBeEquivalentTo(done);

        var approval = AgentRecords.Approval(second.Id, run.Id);
        await Runs.SaveApprovalAsync(approval, Ct);
        (await Runs.GetApprovalAsync(second.Id, Ct)).ShouldBe(approval);
        var decided = approval with { Status = ApprovalStatusDto.Approved, Scope = ApprovalScopeDto.PlanHeavy, DecidedAt = AgentRecords.T0.AddMinutes(1) };
        await Runs.SaveApprovalAsync(decided, Ct);
        (await Runs.GetApprovalAsync(second.Id, Ct)).ShouldBe(decided);
        (await Runs.GetApprovalAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task UndoEntries_AvailableNewestRunFirstStepsReversed()
    {
        var run = await CreateRunAsync();
        var later = AgentRecords.Run(run.ConversationId, AgentRunStatusDto.Completed);
        await Runs.UpdateRunAsync(run with { Status = AgentRunStatusDto.Completed }, Ct);
        await Runs.CreateRunAsync(later, Ct);
        await Undo.AddAsync([AgentRecords.Undo(run.ConversationId, run.Id, "a1"), AgentRecords.Undo(run.ConversationId, run.Id, "a2")], Ct);
        var fresh = AgentRecords.Undo(run.ConversationId, later.Id, "b1") with { Kind = UndoKindDto.Compensate, Fresh = true, ExpectedUpdatedAt = new DateTimeOffset(2026, 11, 16, 9, 30, 0, TimeSpan.FromHours(1)) };
        await Undo.AddAsync([fresh, AgentRecords.Undo(run.ConversationId, later.Id, "b2")], Ct);

        var available = await Undo.GetAvailableAsync(run.ConversationId, Ct);
        available.Select(e => e.Action.Tool).ShouldBe(["b2", "b1", "a2", "a1"]);
        available[1].ShouldBe(fresh);
        available[1].ExpectedUpdatedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        available[0].ExpectedUpdatedAt.ShouldBeNull();

        await Undo.SetStatusAsync(available[0].Id, UndoEntryStatus.Applied, Ct);
        await Undo.SetStatusAsync(available[1].Id, UndoEntryStatus.Obsolete, Ct);
        (await Undo.GetAvailableAsync(run.ConversationId, Ct)).Select(e => e.Action.Tool).ShouldBe(["a2", "a1"]);
        (await Undo.GetAvailableAsync(Guid.NewGuid(), Ct)).ShouldBeEmpty();
        await Should.ThrowAsync<KeyNotFoundException>(() => Undo.SetStatusAsync(Guid.NewGuid(), UndoEntryStatus.Applied, Ct));
    }

    [Fact]
    public async Task Settings_DefaultsUntilSavedThenRoundTrip()
    {
        var defaults = await Settings.GetAsync(Ct);
        defaults.ShouldBeEquivalentTo(AgentSettings.Defaults(AgentBackendDto.Ollama, ApiFactory.Start));

        var changed = defaults with
        {
            Backend = AgentBackendDto.Claude,
            OllamaModel = "qwen3.5:4b",
            OllamaThink = true,
            ClaudeModel = ClaudeModelDto.ClaudeHaiku55,
            ClaudeEffort = ClaudeEffortDto.High,
            ClaudeNoticeAcknowledgedAt = AgentRecords.T0,
            Limits = AgentLimitsDto.Default with { MaxModelTurns = 5 },
            UpdatedAt = AgentRecords.T0.AddDays(1),
        };
        await Settings.SaveAsync(changed, Ct);
        (await Settings.GetAsync(Ct)).ShouldBeEquivalentTo(changed);
        await Settings.SaveAsync(changed with { Backend = AgentBackendDto.None }, Ct);
        (await Settings.GetAsync(Ct)).Backend.ShouldBe(AgentBackendDto.None);
    }

    [Theory]
    [InlineData("none", AgentBackendDto.None)]
    [InlineData("fake", AgentBackendDto.Fake)]
    [InlineData("CLAUDE", AgentBackendDto.Claude)]
    [InlineData("nonsense", AgentBackendDto.Ollama)]
    public async Task SettingsDefaults_UseTheConfiguredBackend(string configured, AgentBackendDto expected)
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { ["StemStudio:Agent:Backend"] = configured } };

        var settings = await factory.Services.GetRequiredService<IAgentSettingsStore>().GetAsync(Ct);

        settings.Backend.ShouldBe(expected);
    }

    [Fact]
    public async Task FakeBackendFromTheEnvironment_OverridesAStoredRow()
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { ["StemStudio:Agent:Backend"] = "fake" } };
        var settings = factory.Services.GetRequiredService<IAgentSettingsStore>();
        await settings.SaveAsync(AgentSettings.Defaults(AgentBackendDto.Ollama, AgentRecords.T0) with { OllamaModel = "qwen3.5:4b" }, Ct);

        var loaded = await settings.GetAsync(Ct);

        (loaded.Backend, loaded.OllamaModel).ShouldBe((AgentBackendDto.Fake, "qwen3.5:4b"));
    }

    [Fact]
    public async Task StartUp_InterruptsNonTerminalRunsAndExpiresPendingApprovals()
    {
        var dataDir = Directory.CreateTempSubdirectory("stemstudio-api-").FullName;
        Seeded seeded;
        using (var before = new ApiFactory(dataDir))
        {
            seeded = await SeedSuspendedRunAsync(before.Services);
        }

        try
        {
            await AssertRecoveredAsync(dataDir, seeded);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(dataDir, true);
        }
    }

    private static async Task AssertRecoveredAsync(string dataDir, Seeded seeded)
    {
        var (conversationId, runId, stepId, doneRunId) = seeded;
        using var after = new ApiFactory(dataDir);
        var runs = after.Services.GetRequiredService<IRunStore>();
        var run = (await runs.GetRunAsync(runId, Ct))!;
        (run.Status, run.EndedAt).ShouldBe((AgentRunStatusDto.Interrupted, ApiFactory.Start));
        (await runs.GetRunAsync(doneRunId, Ct))!.Status.ShouldBe(AgentRunStatusDto.Completed);
        (await runs.GetApprovalAsync(stepId, Ct))!.Status.ShouldBe(ApprovalStatusDto.Expired);
        (await runs.GetStepsAsync(runId, Ct)).Single().Status.ShouldBe(AgentStepStatusDto.Expired);
        (await after.Services.GetRequiredService<IConversationStore>().GetAsync(conversationId, Ct))!.ActiveRunId.ShouldBeNull();
        (await runs.GetActiveRunAsync(Ct)).ShouldBeNull();
        var events = await after.Services.GetRequiredService<IRunEventLogReader>().ReadAfterAsync(runId, 0, Ct);
        events.Single().Type.ShouldBe(AgentEventTypes.RunInterrupted);
        events.Single().Payload.ShouldBe($$"""{"runId":"{{runId}}"}""");
    }

    private static async Task<Seeded> SeedSuspendedRunAsync(IServiceProvider services)
    {
        var conversations = services.GetRequiredService<IConversationStore>();
        var runs = services.GetRequiredService<IRunStore>();
        var conversation = AgentRecords.Conversation();
        await conversations.CreateAsync(conversation, Ct);
        var done = AgentRecords.Run(conversation.Id, AgentRunStatusDto.Completed);
        await runs.CreateRunAsync(done, Ct);
        var run = AgentRecords.Run(conversation.Id, AgentRunStatusDto.AwaitingApproval);
        await runs.CreateRunAsync(run, Ct);
        await conversations.UpdateAsync(conversation with { ActiveRunId = run.Id }, Ct);
        var step = AgentRecords.Step(run.Id, 1, AgentStepStatusDto.AwaitingApproval);
        await runs.SaveStepAsync(step, Ct);
        await runs.SaveApprovalAsync(AgentRecords.Approval(step.Id, run.Id), Ct);
        return new Seeded(conversation.Id, run.Id, step.Id, done.Id);
    }

    private async Task<AgentRun> CreateRunAsync()
    {
        var conversation = AgentRecords.Conversation();
        await Conversations.CreateAsync(conversation, Ct);
        var run = AgentRecords.Run(conversation.Id);
        await Runs.CreateRunAsync(run, Ct);
        return run;
    }

    private sealed record Seeded(Guid ConversationId, Guid RunId, Guid StepId, Guid DoneRunId);
}
