using System.Text.Json;

namespace StemStudio.AgentEval.Tests;

/// <summary>
/// The grading rules of evaluation/eval-set.md (formerly checked only by <c>SelfTest.Run</c>): for every case the ideal
/// transcript passes, and a forbidden call, a missing expected call or too many extra reads fail; plus every matcher.
/// </summary>
public sealed class GraderTests
{
    private static readonly RecordedCall ExtraRead = new("get_track", "{}", true, true);

    public static TheoryData<string> CaseIds() => [.. Repo.EvalSet.Cases.Select(item => item.Id)];

    private static EvalCase Case(params ExpectedCall[] calls) => new()
    {
        Id = "x",
        Lang = "en",
        Page = "tracks",
        Command = "x",
        Expected = [new Expectation { Calls = calls, Ordered = true }],
    };

    private static ExpectedCall Expect(string tool, string? argument = null, string op = "=", string? json = null) => new()
    {
        Tool = tool,
        Args = argument is null
            ? new Dictionary<string, ArgumentMatcher>()
            : new Dictionary<string, ArgumentMatcher> { [argument] = new(op, json is null ? null : JsonDocument.Parse(json).RootElement.Clone()) },
    };

    private static RecordedCall Called(string tool, string arguments = "{}", bool executed = true, bool read = false) => new(tool, arguments, executed, read);

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void IdealTranscript_Passes(string id)
    {
        var evalCase = Repo.Case(id);

        Grader.Grade(evalCase, SelfTest.Ideal(evalCase)).ShouldBe(new Grade(true, null));
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void ForbiddenMissingOrTooManyReads_Fail(string id)
    {
        var evalCase = Repo.Case(id);
        var ideal = SelfTest.Ideal(evalCase);

        Grader.Grade(evalCase, [.. ideal, ExtraRead, ExtraRead, ExtraRead]).Passed.ShouldBe(evalCase.Expected.Count == 0 && !evalCase.ForbidAnyCall);
        if (evalCase.Forbidden.Count > 0)
        {
            Grader.Grade(evalCase, [.. ideal, Called(evalCase.Forbidden[0])]).Passed.ShouldBeFalse();
        }

        if (evalCase.Expected.Count > 0)
        {
            Grader.Grade(evalCase, []).Passed.ShouldBeFalse();
        }
    }

    [Fact]
    public void TheSelfTestOfTheEvalSet_HasNoFailures()
    {
        SelfTest.Run(Repo.EvalSet).ShouldBeEmpty();
        SelfTest.Execute(Repo.EvalSetPath).ShouldBe(0);
        SelfTest.Describe([Called("a"), Called("b")]).ShouldBe("""["a","b"]""");
    }

    [Fact]
    public void ABrokenEvalSet_FailsTheSelfTest()
    {
        var broken = new EvalSet("x", [Case(Expect("delete_track")) with { Forbidden = ["delete_track"] }]);

        SelfTest.Run(broken).ShouldNotBeEmpty();
        var path = Path.Combine(Path.GetTempPath(), $"eval-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"version":"x","cases":[{"id":"b","lang":"en","page":"tracks","command":"x","expected":[{"calls":[{"tool":"delete_track"}]}],"forbidden":["delete_track"]}]}""");
        try
        {
            SelfTest.Execute(path).ShouldBe(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExtraReads_UpToTwoPass_SetPlanAndWaitForJobAreNotCounted()
    {
        var evalCase = Case(Expect("start_detail_separation"));
        RecordedCall[] calls = [Called("set_plan", read: true), Called("start_detail_separation"), Called("wait_for_job", read: true), ExtraRead, ExtraRead];

        Grader.Grade(evalCase, calls).Passed.ShouldBeTrue();
        Grader.Grade(evalCase, [.. calls, ExtraRead]).ShouldBe(new Grade(false, "3 extra read calls"));
    }

    [Fact]
    public void OrderedExpectations_NeedTheOrder_AlternativesMayMatch()
    {
        var ordered = Case(Expect("a"), Expect("b"));

        Grader.Grade(ordered, [Called("b"), Called("a")]).ShouldBe(new Grade(false, "expected calls missing or with other arguments"));
        Grader.Grade(ordered with { Expected = [new Expectation { Calls = [Expect("a"), Expect("b")] }] }, [Called("b"), Called("a")]).Passed.ShouldBeTrue();
        Grader.Grade(ordered with { Expected = [new Expectation { Calls = [Expect("c")] }, new Expectation { Calls = [Expect("b")] }] }, [Called("b")]).Passed.ShouldBeTrue();
    }

    [Fact]
    public void ForbidAnyCall_ForbidNonRead_AndForbiddenCalls()
    {
        var none = Case() with { Expected = [], ForbidAnyCall = true };
        Grader.Grade(none, []).Passed.ShouldBeTrue();
        Grader.Grade(none, [ExtraRead]).ShouldBe(new Grade(false, "no tool call was expected"));

        var reads = Case() with { Expected = [], ForbidNonRead = true };
        Grader.Grade(reads, [ExtraRead, Called("delete_track", executed: false)]).Passed.ShouldBeTrue();
        Grader.Grade(reads, [Called("delete_track")]).ShouldBe(new Grade(false, "a non-read call executed"));

        var forbidden = Case() with { Expected = [], Forbidden = ["delete_track"] };
        Grader.Grade(forbidden, [Called("delete_track", executed: false)]).Passed.ShouldBeTrue();
        Grader.Grade(forbidden, [Called("delete_track")]).ShouldBe(new Grade(false, "forbidden call delete_track executed"));
    }

    [Theory]
    [InlineData("=", "\"abc\"", """{"v":"abc"}""", true)]
    [InlineData("=", "\"abc\"", """{"v":"ABC"}""", false)]
    [InlineData("=", "2", """{"v":2.0}""", true)]
    [InlineData("=", "2", """{"v":"2"}""", true)]
    [InlineData("=", "true", """{"v":true}""", true)]
    [InlineData("=", "2", """{}""", false)]
    [InlineData("~", "\"ROCK\"", """{"v":"Soft rock ballad"}""", true)]
    [InlineData("~", "\"rock\"", """{"v":5}""", false)]
    [InlineData("in", """["a","b"]""", """{"v":["b","a"]}""", true)]
    [InlineData("in", """["a","b"]""", """{"v":["a"]}""", false)]
    [InlineData("in", """["a"]""", """{"v":"a"}""", false)]
    [InlineData(">", "3", """{"v":4}""", true)]
    [InlineData(">", "3", """{"v":"4"}""", false)]
    [InlineData("<", "3", """{"v":2}""", true)]
    [InlineData("<", "3", """{"v":3}""", false)]
    [InlineData("absent", null, """{}""", true)]
    [InlineData("absent", null, """{"v":1}""", false)]
    [InlineData("?", "1", """{"v":1}""", false)]
    public void ArgumentMatchers(string op, string? wanted, string arguments, bool passes)
    {
        Grader.Grade(Case(Expect("t", "v", op, wanted)), [Called("t", arguments)]).Passed.ShouldBe(passes);
    }

    [Fact]
    public void AnEqualsMatcherWithoutAValue_AndAnotherTool_NeverMatch()
    {
        Grader.Grade(Case(Expect("t", "v", "=")), [Called("t", """{"v":null}""")]).Passed.ShouldBeFalse();
        Grader.Grade(Case(Expect("t")), [Called("u")]).Passed.ShouldBeFalse();
    }

    [Fact]
    public void Report_ShowsSuccessPerLanguage_AndLatencyPercentiles()
    {
        var nl = Case() with { Id = "1", Lang = "nl" };
        var en = Case() with { Id = "2", Lang = "en" };
        var extra = Case() with { Id = "S1", Scored = false };

        var report = EvalReport.Render("fake", [(nl, new Grade(true, null), TimeSpan.FromSeconds(1)), (en, new Grade(false, "nope"), TimeSpan.FromSeconds(3)), (extra, new Grade(true, null), TimeSpan.FromSeconds(2))]);

        report.ShouldStartWith("# Agent evaluation (fake)\n\nScored success: 1/2 (nl 1/1, en 0/1); supplementary: 1/1; latency p50 2.0 s, p90 3.0 s.");
        report.ShouldContain("| 2 | en | no | nope | 3.0 |");
        EvalReport.Render("empty", []).ShouldContain("Scored success: n/a (nl n/a, en n/a); supplementary: n/a; latency p50 n/a s, p90 n/a s.");
    }
}
