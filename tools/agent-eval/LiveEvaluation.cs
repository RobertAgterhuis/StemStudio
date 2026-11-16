namespace StemStudio.AgentEval;

/// <summary>
/// A live evaluation: every case <c>runs</c> times against the backend over a fresh fixture, the median grade per case
/// (passes in more than half of the runs; SC-001 uses 3 runs) and the median latency, written as the Markdown report.
/// </summary>
internal static class LiveEvaluation
{
    /// <summary>The undo case's prelude (its command changes the mixer first).</summary>
    public const string UndoPreludeCase = "8";

    /// <summary>Runs the set and returns the per-case results (median over the runs).</summary>
    public static async Task<IReadOnlyList<(EvalCase Case, Grade Grade, TimeSpan Latency)>> RunAsync(EvalSet set, LiveRunner runner, int runs, TextWriter log)
    {
        var results = new List<(EvalCase Case, Grade Grade, TimeSpan Latency)>();
        foreach (var evalCase in set.Cases)
        {
            var attempts = await AttemptsAsync(evalCase, runner, runs);
            var result = Median(evalCase, attempts);
            var passed = attempts.Count(attempt => attempt.Grade.Passed);
            await log.WriteLineAsync($"case {evalCase.Id}: {(result.Grade.Passed ? "pass" : "FAIL")} {passed}/{runs} {result.Grade.Reason} ({string.Join(" ", attempts.Select(Calls))})");
            results.Add(result);
        }

        return results;
    }

    /// <summary>Writes the report and returns the process exit code (0 when every case passed).</summary>
    public static async Task<int> ReportAsync(string title, string path, IReadOnlyList<(EvalCase Case, Grade Grade, TimeSpan Latency)> results)
    {
        await File.WriteAllTextAsync(path, EvalReport.Render(title, results));
        var failed = results.Count(result => !result.Grade.Passed);
        Console.WriteLine($"agent-eval live ({title}): {results.Count} cases, {failed} failed; report {path}.");
        return failed == 0 ? 0 : 1;
    }

    private static async Task<List<CaseRun>> AttemptsAsync(EvalCase evalCase, LiveRunner runner, int runs)
    {
        var attempts = new List<CaseRun>();
        for (var run = 0; run < runs; run++)
        {
            attempts.Add(await runner.RunAsync(evalCase));
        }

        return attempts;
    }

    private static (EvalCase Case, Grade Grade, TimeSpan Latency) Median(EvalCase evalCase, List<CaseRun> attempts)
    {
        var passed = attempts.Count(attempt => attempt.Grade.Passed);
        var latency = attempts.Select(attempt => attempt.Latency).Order().ElementAt(attempts.Count / 2);
        var reason = attempts.FirstOrDefault(attempt => !attempt.Grade.Passed)?.Grade.Reason;
        var grade = passed * 2 > attempts.Count ? new Grade(true, null) : new Grade(false, $"{passed}/{attempts.Count} runs passed; {reason}");
        return (evalCase, grade, latency);
    }

    private static string Calls(CaseRun run) => SelfTest.Describe(run.Calls) + (run.Cards > 0 ? $" cards:{run.Cards}" : string.Empty);
}
