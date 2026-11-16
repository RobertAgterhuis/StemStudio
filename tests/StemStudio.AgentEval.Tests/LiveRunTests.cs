using NSubstitute;
using StemStudio.Contracts.Agent;

namespace StemStudio.AgentEval.Tests;

/// <summary>
/// A fake live run: every case plays its ideal transcript through the real agent runtime over the seeded fixture (no model),
/// so the fixture, the approval answers, the undo case and the grading are checked end to end.
/// </summary>
public sealed class LiveRunTests
{
    private static LiveRunner Runner(EvalCase? prelude) => new(EvalBackends.Fake(prelude), Repo.Presets, prelude);

    [Fact]
    public async Task EveryCase_PassesWithItsIdealTranscript()
    {
        var prelude = Repo.Case(LiveEvaluation.UndoPreludeCase);
        using var log = new StringWriter();

        var results = await LiveEvaluation.RunAsync(Repo.EvalSet, Runner(prelude), 1, log);

        results.Where(result => !result.Grade.Passed).Select(result => $"{result.Case.Id}: {result.Grade.Reason}").ShouldBeEmpty(log.ToString());
        results.Count.ShouldBe(Repo.EvalSet.Cases.Count);
    }

    [Theory]
    [InlineData("17", "retry_track")]
    [InlineData("27", "export_audio")]
    public async Task LibraryTitlesTaintTheRun_SoTheChangeAfterASearchIsApprovedAndExecuted(string id, string tool)
    {
        var evalCase = WithSearchFirst(Repo.Case(id));

        var run = await Runner(null).RunAsync(evalCase);

        evalCase.Approval.ShouldBe(ApprovalAnswer.Approve);
        run.Cards.ShouldBeGreaterThan(0);
        run.Calls.Single(call => call.Tool == tool).Executed.ShouldBeTrue();
        run.Grade.Passed.ShouldBeTrue();
    }

    /// <summary>The case's first expectation with a leading <c>search_library</c> (as a real model does; eval-set.md).</summary>
    private static EvalCase WithSearchFirst(EvalCase evalCase)
    {
        var first = evalCase.Expected[0];
        return first.Calls[0].Tool == "search_library"
            ? evalCase
            : evalCase with { Expected = [first with { Calls = [new ExpectedCall { Tool = "search_library" }, .. first.Calls] }] };
    }

    [Fact]
    public async Task TheUndoCase_WithoutItsPrelude_Fails()
    {
        var run = await Runner(null).RunAsync(Repo.EvalSet.Cases.Single(item => item.Undo));

        run.Grade.ShouldBe(new Grade(false, "the undo case needs case 8 as its prelude"));
    }

    [Fact]
    public async Task AnUndoWithNothingToUndo_FailsOnlyThatCase()
    {
        var readOnly = Repo.Case("21");

        var run = await Runner(readOnly).RunAsync(Repo.EvalSet.Cases.Single(item => item.Undo));

        run.Grade.ShouldBe(new Grade(false, "AgentConflictException: There is nothing to undo."));
    }

    [Fact]
    public async Task AFailingBackend_FailsTheCaseWithTheRunFailure()
    {
        var session = Substitute.For<StemStudio.Agent.Runtime.IAgentChatSession>();
        session.NextTurnAsync(default!, TestContext.Current.CancellationToken).ReturnsForAnyArgs<Task<StemStudio.Agent.Runtime.ChatTurn>>(_ => throw new HttpRequestException("connection refused"));
        var failing = new LiveBackend(AgentBackendDto.Ollama, "broken", (_, _) => session);

        var run = await new LiveRunner(failing, Repo.Presets, null).RunAsync(Repo.Case("1"));

        run.Grade.Passed.ShouldBeFalse();
        run.Grade.Reason!.ShouldStartWith("run Failed");
    }

    [Fact]
    public async Task Report_IsWritten_AndTheExitCodeCountsFailures()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agent-eval-{Guid.NewGuid():N}.md");
        var evalCase = Repo.Case("1");
        try
        {
            (await LiveEvaluation.ReportAsync("t", path, [(evalCase, new Grade(true, null), TimeSpan.Zero)])).ShouldBe(0);
            (await LiveEvaluation.ReportAsync("t", path, [(evalCase, new Grade(false, "x"), TimeSpan.Zero)])).ShouldBe(1);
            File.ReadAllText(path).ShouldContain("| 1 | nl | no | x |");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task EntryPoint_FakeBackend_RunsTheSelfTestAndTheLiveRun()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agent-eval-{Guid.NewGuid():N}.md");
        try
        {
            (await EntryPoint.RunAsync("--backend", "fake", "--runs", "1", "--eval-set", Repo.EvalSetPath, "--presets", Repo.PresetsPath, "--out", path)).ShouldBe(0);
            File.ReadAllText(path).ShouldContain("Scored success: 30/30");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
