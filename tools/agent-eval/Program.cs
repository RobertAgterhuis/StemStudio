// Skeleton (architect, 011 Phase 0). The ai-agent implements T079a: fake StemStudio actions seeded from the eval-set fixture,
// the grading rules of evaluation/eval-set.md, the Markdown report (success per case and language, latency p50/p90).
// The Claude backend is paid and opt-in (constitution 1.3.0): it runs only on the owner's explicit --backend claude.
using StemStudio.AgentEval;

var (options, error) = EvalOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine(error);
    return 64;
}

Console.WriteLine($"agent-eval: backend={options.Backend} runs={options.Runs} set={options.EvalSetPath} report={options.ReportPath}");
Console.Error.WriteLine("The evaluation harness is not implemented yet (tasks.md T079a, owner ai-agent).");
return 2;
