using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tests.Fakes;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;
using static StemStudio.Agent.Tests.Fakes.TestTools;

namespace StemStudio.Agent.Tests.Runtime;

/// <summary>
/// Security fix (coordinator finding on T025): no exception may leave a run non-terminal or the conversation blocked; raw
/// exception texts never reach the run, the events or the model, only the server log.
/// </summary>
public sealed class RunnerRobustnessTests
{
    private const string Secret = "db-password=hunter2";

    private static async Task<bool> ReleasedAsync(RunnerHarness harness) =>
        (await harness.Conversations.GetAsync(harness.ConversationId, CancellationToken.None))!.ActiveRunId is null;

    private static void NoSecretAnywhere(RunnerHarness harness, AgentRun run)
    {
        harness.Events.Events.ShouldAllBe(item => !item.Payload.Contains(Secret));
        harness.Runs.Steps.ShouldAllBe(step => step.Error == null || !step.Error.Contains(Secret));
        (run.FailureMessage ?? string.Empty).ShouldNotContain(Secret);
        harness.Script.Requests.SelectMany(request => request.Messages).ShouldAllBe(message => !(message.Text ?? string.Empty).Contains(Secret));
    }

    [Fact]
    public async Task ThrowingHandler_FailsOnlyTheStep()
    {
        var tool = Server("buggy", _ => throw new InvalidOperationException(Secret));
        var harness = RunnerHarness.Scripted([tool], Turn(Call("buggy")), Say("Sorry."));

        var run = await harness.ExecuteAsync();

        run.Status.ShouldBe(AgentRunStatusDto.Completed);
        var step = harness.Runs.Steps.Single();
        (step.Status, step.Error).ShouldBe((AgentStepStatusDto.Failed, "The step failed because of an internal error."));
        NoSecretAnywhere(harness, run);
        harness.Script.Requests[1].Messages.SelectMany(m => m.Contents).OfType<Microsoft.Extensions.AI.FunctionResultContent>().Single().Result!.ToString()!.ShouldContain("internal error");
    }

    [Fact]
    public async Task UnreachableApiInAHandler_FailsTheStepWithAReason()
    {
        var tool = Server("remote", _ => throw new HttpRequestException(Secret));
        var harness = RunnerHarness.Scripted([tool], Turn(Call("remote")), Say("ok"));

        var run = await harness.ExecuteAsync();

        run.Status.ShouldBe(AgentRunStatusDto.Completed);
        harness.Runs.Steps.Single().Error.ShouldBe("StemStudio could not be reached.");
        NoSecretAnywhere(harness, run);
    }

    [Fact]
    public async Task TimeoutInAHandler_FailsTheStepWithAReason()
    {
        var tool = Server("slow", _ => throw new TimeoutException(Secret));
        var harness = RunnerHarness.Scripted([tool], Turn(Call("slow")), Say("ok"));

        var run = await harness.ExecuteAsync();

        harness.Runs.Steps.Single().Error.ShouldBe("The step timed out.");
        NoSecretAnywhere(harness, run);
    }

    [Fact]
    public async Task MalformedUiResult_FailsTheStep()
    {
        var harness = RunnerHarness.Scripted([Ui("press")], Turn(Call("press")), Say("ok"));
        harness.Ui.Responder = _ => new UiToolResult { Ok = true, Result = "{not json" };

        var run = await harness.ExecuteAsync();

        run.Status.ShouldBe(AgentRunStatusDto.Completed);
        var step = harness.Runs.Steps.Single();
        (step.Status, step.Error).ShouldBe((AgentStepStatusDto.Failed, "The page returned an invalid result."));
    }

    [Fact]
    public async Task CancellationMidStep_CancelsTheRunAndReleasesTheConversation()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = Server("hang", _ => ToolResult.Of(new JsonObject())) with
        {
            Handler = async (_, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return ToolResult.Of(new JsonObject());
            },
        };
        var harness = RunnerHarness.Scripted([tool], Turn(Call("hang")), Say("never"));
        await harness.SeedAsync();

        var execution = harness.Runner().ExecuteAsync(harness.Run.Id, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await cancel.CancelAsync();
        await execution.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var run = (await harness.CurrentRunAsync())!;
        run.Status.ShouldBe(AgentRunStatusDto.Cancelled);
        run.EndedAt.ShouldNotBeNull();
        harness.Events.Types[^1].ShouldBe(AgentEventTypes.RunCancelled);
        (await ReleasedAsync(harness)).ShouldBeTrue();
    }

    [Fact]
    public async Task UnexpectedChatException_FailsWithAGenericMessage_AndLogsTheDetail()
    {
        var logger = new CapturingLogger();
        var harness = new RunnerHarness(new ThrowingChatClient(new NotSupportedException(Secret)), [Echo()]) { Logger = logger };

        var run = await harness.ExecuteAsync();

        (run.Status, run.FailureCode, run.FailureMessage).ShouldBe((AgentRunStatusDto.Failed, AgentFailureCodes.Internal, "The run failed because of an internal error."));
        harness.Events.Events.ShouldAllBe(item => !item.Payload.Contains(Secret));
        harness.Events.Types[^1].ShouldBe(AgentEventTypes.RunFailed);
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Error && entry.Text.Contains(Secret));
        (await ReleasedAsync(harness)).ShouldBeTrue();
    }

    [Fact]
    public async Task ThrowingStoreDuringTheRun_StillEndsAndReleasesOnce()
    {
        var harness = RunnerHarness.Scripted([Echo()], Turn(Call("echo")), Say("ok"));
        var store = new FlakyRunStore(harness.Runs, failOnUpdate: 3);
        harness.RunStore = store;

        var run = await harness.ExecuteAsync();

        (run.Status, run.FailureCode).ShouldBe((AgentRunStatusDto.Failed, AgentFailureCodes.Internal));
        run.FailureMessage!.ShouldNotContain(Secret);
        harness.Events.Types.Count(AgentEventTypes.IsTerminal).ShouldBe(1);
        (await ReleasedAsync(harness)).ShouldBeTrue();
        harness.Conversations.Updates.ShouldBe(1);
    }

    [Fact]
    public async Task StoreThatAlwaysFailsAtTheEnd_StillReleasesTheConversation()
    {
        var harness = RunnerHarness.Scripted([Echo()], Say("ok"));
        harness.RunStore = new FlakyRunStore(harness.Runs, failOnUpdate: 2, keepFailing: true);

        await harness.SeedAsync();
        await harness.Runner().ExecuteAsync(harness.Run.Id, TestContext.Current.CancellationToken);

        (await ReleasedAsync(harness)).ShouldBeTrue();
        harness.Events.Types.Count(AgentEventTypes.IsTerminal).ShouldBe(1);
    }

    /// <summary>A run store whose UpdateRunAsync throws on the n-th call (and optionally every later call).</summary>
    private sealed class FlakyRunStore(InMemoryRunStore inner, int failOnUpdate, bool keepFailing = false) : IRunStore
    {
        private int _updates;

        public Task CreateRunAsync(AgentRun run, CancellationToken cancellationToken) => inner.CreateRunAsync(run, cancellationToken);

        public Task<AgentRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken) => inner.GetRunAsync(runId, cancellationToken);

        public Task UpdateRunAsync(AgentRun run, CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _updates);
            return count == failOnUpdate || (keepFailing && count > failOnUpdate)
                ? throw new InvalidOperationException(Secret)
                : inner.UpdateRunAsync(run, cancellationToken);
        }

        public Task<AgentRun?> GetActiveRunAsync(CancellationToken cancellationToken) => inner.GetActiveRunAsync(cancellationToken);

        public Task SaveStepAsync(RunStep runStep, CancellationToken cancellationToken) => inner.SaveStepAsync(runStep, cancellationToken);

        public Task<IReadOnlyList<RunStep>> GetStepsAsync(Guid runId, CancellationToken cancellationToken) => inner.GetStepsAsync(runId, cancellationToken);

        public Task SaveApprovalAsync(Approval approval, CancellationToken cancellationToken) => inner.SaveApprovalAsync(approval, cancellationToken);

        public Task<Approval?> GetApprovalAsync(Guid stepId, CancellationToken cancellationToken) => inner.GetApprovalAsync(stepId, cancellationToken);
    }
}
