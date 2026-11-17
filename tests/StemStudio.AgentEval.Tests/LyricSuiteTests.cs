using System.Text.RegularExpressions;
using StemStudio.Agent.LyricAssist;
using StemStudio.AgentEval.Lyric;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.AgentEval.Tests;

/// <summary>023 T066: the lyric suite (JSON in sync with the Markdown set, grading, self-test with a scripted model, CLI).</summary>
public sealed partial class LyricSuiteTests
{
    private static readonly string MarkdownPath = Path.Combine(Repo.Root, "specs", "023-lyric-writing-assistant", "evaluation", "lyric-eval-set.md");
    private static readonly string JsonPath = Path.Combine(Repo.Root, "tools", "agent-eval", "lyric-eval-set.json");
    private static readonly LyricEvalSet Set = LyricEvalSet.Parse(File.ReadAllText(JsonPath));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LyricCase Case(string id) => Set.Cases.Single(item => item.Id == id);

    [Fact]
    public void TheJson_HasEveryCaseOfTheMarkdownSet_InOrder()
    {
        var markdown = File.ReadAllText(MarkdownPath);

        Set.Cases.Select(item => item.Id).ShouldBe(Ids(markdown, CaseRow()));
        Set.Guardrails.Select(item => item.Id).ShouldBe(Ids(markdown, GuardRow()));
        Set.Performance.Select(item => item.Id).ShouldBe(Ids(markdown, PerformanceRow()));
        Set.Cases.Count.ShouldBe(64);
        Set.Cases.Count(item => item.Lang == "nl").ShouldBe(32);
        Set.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public void Cases_AreConvertedWithAnchorsTrapsAndKinds()
    {
        Case("N01").Expected.Single().ShouldBe(new ExpectedCorrection { Anchor = "wiet", Proposals = ["weet"] }, new ExpectedCorrectionComparer());
        Case("N21").Forbidden.Select(trap => trap.Span).ShouldBe(["'k", "'t"]);
        Case("N04").Forbidden.Single().Any.ShouldBeTrue();
        Case("E20").Forbidden.Single().Line.ShouldBe(4);
        Case("N30").RhythmTarget!.SyllablesPerBeat.ShouldBe(1.5);
        Case("N31").AssistKind.ShouldBe(AssistKindDto.Alternatives);
        Case("E32").Count.ShouldBe(5);
        Case("E01").Language.ShouldBe(AssistLanguageDto.En);
        Case("N30").Lines[0].ShouldNotContain("(12+)");
        Set.Cases.ShouldAllBe(item => item.Lines.Count <= LyricAssistLimits.DefaultBlockLines);
    }

    [Fact]
    public async Task SelfTest_IdealOutputsPass_AndTheTrapModelIsCaught()
    {
        var log = new StringWriter();

        var failures = await LyricSuite.SelfTestAsync(Set, log, Token);

        failures.ShouldBeEmpty(log.ToString());
        log.ToString().ShouldContain("64 cases, 0 grading failures");
    }

    [Fact]
    public async Task Grading_CountsRecallPrecisionAndLicenceFalsePositives()
    {
        var ideal = await LyricSuite.RunAsync(Set, LyricSuite.IdealModel, 1, TextWriter.Null, Token);
        var traps = await LyricSuite.RunAsync(Set, LyricSuite.TrapModel, 1, TextWriter.Null, Token);
        var nothing = await LyricSuite.RunAsync(Set, _ => new ScriptedNothing(), 1, TextWriter.Null, Token);

        LyricGrader.Recall(ideal).ShouldBe(1);
        LyricGrader.Recall(nothing).ShouldBe(0);
        LyricGrader.Precision(nothing).ShouldBe(1);
        LyricGrader.Precision(traps).ShouldBe(0);
        LyricGrader.LicenceFalsePositives(traps).ShouldBeGreaterThan(0.9);
        nothing.Where(grade => grade.CreativePassed is not null).ShouldAllBe(grade => grade.CreativePassed == false);
        var report = LyricGrader.Report("test", ideal);
        report.ShouldContain("| nl | 100 % | 100 % | 0 % |");
        report.ShouldContain("| en licence |");
        report.ShouldContain("Latency per case: p50");
        LyricGrader.Report("empty", []).ShouldContain("p50 0.0 s");
    }

    [Fact]
    public void TheJob_OfACase_IsOneBlockWithTheKindsRange()
    {
        var alternatives = LyricSuite.Job(Case("N31"));
        var continuation = LyricSuite.Job(Case("N32"));

        alternatives.Blocks.Single().LastLine.ShouldBe(2);
        (alternatives.LineFrom, alternatives.LineTo, alternatives.Count).ShouldBe((2, 2, 3));
        (continuation.LineFrom, continuation.LineTo).ShouldBe((null, 2));
        LyricSuite.Job(Case("N01")).Title.ShouldBe("Eval N01");
    }

    [Fact]
    public async Task AFailingAssist_IsGradedAsEmpty()
    {
        var grade = await LyricSuite.RunCaseAsync(Case("N01"), new FailingModel(), Token);

        (grade.Found, grade.Expected).ShouldBe((0, 1));
    }

    [Fact]
    public void Blocks_SplitAtBlankLinesAndTheBlockSize()
    {
        var text = string.Join('\n', ["[verse]", "a", "b", "", "", "[chorus]", .. Enumerable.Range(1, 10).Select(i => $"l{i}"), ""]);

        var blocks = LyricPerformance.Blocks(text, 8);

        blocks.Select(block => (block.FirstLine, block.LastLine)).ShouldBe([(1, 3), (6, 13), (14, 16)]);
        blocks.Select(block => block.Index).ShouldBe([0, 1, 2]);
        LyricPerformance.Blocks("one\r\ntwo", 8).Single().LastLine.ShouldBe(2);
    }

    [Fact]
    public async Task APerformanceRun_TimesEveryBlock_AndCountsValidJson()
    {
        var sheet = await File.ReadAllTextAsync(Path.Combine(Repo.Root, "tests", "fixtures", "lyrics", "assist", "sheets", "p1-havenlicht.nl.txt"), Token);

        var run = await LyricPerformance.RunAsync(sheet, AssistLanguageDto.Nl, new ScriptedNothing(), Token);
        var report = LyricPerformance.Report("test", [run, run with { BlockSeconds = [] }]);

        run.BlockSeconds.Count.ShouldBe(LyricPerformance.Blocks(sheet, 8).Count);
        (run.Replies, run.ValidReplies).ShouldBe((run.BlockSeconds.Count, run.BlockSeconds.Count));
        run.TotalSeconds.ShouldBeGreaterThanOrEqualTo(run.FirstBlockSeconds);
        report.ShouldContain("| 1 |");
        report.ShouldContain("Valid JSON");
    }

    [Fact]
    public void Options_ParseTheSheetAndTheCaseFilter()
    {
        var options = EvalOptions.Parse(["--suite", "lyric", "--sheet", "p1.nl.txt", "--cases", "N01, N02"]).Options!;

        options.Sheet.ShouldBe("p1.nl.txt");
        options.Cases.ShouldBe(["N01", "N02"]);
    }

    [Fact]
    public void HarnessVerification_ApproximatesSyllablesAndRhymes()
    {
        var verification = new HarnessVerification();

        verification.CountSyllables("[chorus]", AssistLanguageDto.En).ShouldBe(0);
        verification.CountSyllables("de zomeravond", AssistLanguageDto.Nl).ShouldBe(5);
        verification.Rhyme("night", "light", AssistLanguageDto.En).ShouldBe(RhymeKindDto.Perfect);
        verification.Rhyme("night", "day", AssistLanguageDto.En).ShouldBeNull();
        verification.Rhyme("...", "...", AssistLanguageDto.En).ShouldBeNull();
        verification.IsVerbatim("anything").ShouldBeFalse();
    }

    [Fact]
    public async Task EntryPoint_LyricSuiteWithTheFakeBackend_RunsTheSelfTest()
    {
        (await EntryPoint.RunAsync("--suite", "lyric", "--backend", "fake", "--lyric-set", JsonPath)).ShouldBe(0);
        EvalOptions.Parse(["--suite", "poems"]).Options.ShouldBeNull();
        EvalOptions.Parse(["--suite", "lyric"]).Options!.Suite.ShouldBe("lyric");
        EvalOptions.Parse([]).Options!.Paths.LyricSet.ShouldBe("tools/agent-eval/lyric-eval-set.json");
    }

    [Fact]
    public async Task TheReferenceClient_UsesOllamaOnly()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:9/") };

        var client = LyricCommand.Client("qwen3.5:4b", http);

        (await client.SelectAsync(Token)).ShouldBe(new LyricModelInfo { Backend = Contracts.Agent.AgentBackendDto.Ollama, Model = "qwen3.5:4b", UsingFallback = false });
    }

    private static List<string> Ids(string markdown, Regex row) => [.. row.Matches(markdown).Select(match => match.Groups[1].Value)];

    [GeneratedRegex(@"^\| ([NE]\d\d) \|", RegexOptions.Multiline)]
    private static partial Regex CaseRow();

    [GeneratedRegex(@"^\| (L-[A-Z]\d) \|", RegexOptions.Multiline)]
    private static partial Regex GuardRow();

    [GeneratedRegex(@"^\| (P\d) \|", RegexOptions.Multiline)]
    private static partial Regex PerformanceRow();

    private sealed class ScriptedNothing : ILyricModel
    {
        public Task<LyricModelReply> CompleteAsync(LyricModelRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new LyricModelReply { Json = """{"items":[]}""", Model = StemStudio.Agent.LyricAssist.Fakes.ScriptedLyricModel.Info });
    }

    private sealed class FailingModel : ILyricModel
    {
        public Task<LyricModelReply> CompleteAsync(LyricModelRequest request, CancellationToken cancellationToken) =>
            throw new LyricAssistException(AssistFailureCodeDto.BackendUnavailable, "down");
    }

    private sealed class ExpectedCorrectionComparer : IEqualityComparer<ExpectedCorrection>
    {
        public bool Equals(ExpectedCorrection? x, ExpectedCorrection? y) =>
            x?.Anchor == y?.Anchor && x!.Proposals.SequenceEqual(y!.Proposals) && x.Types.SequenceEqual(y.Types);

        public int GetHashCode(ExpectedCorrection obj) => obj.Anchor?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}
