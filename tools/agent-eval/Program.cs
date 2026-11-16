// The evaluation harness of the composer agent (feature 011, research R-09, T079a). `--backend fake` runs the grading
// self-test over evaluation/eval-set.md as data (eval-set.json) without any model. Live runs against Ollama or Claude
// (paid, opt-in, constitution 1.3.0: only on the owner's explicit --backend claude) need the seeded fixture actions, which
// are not implemented yet (see docs/handoffs/ai-agent-011-polish.md).
using StemStudio.AgentEval;

var (options, error) = EvalOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine(error);
    return 64;
}

if (options.Backend == "fake")
{
    return SelfTest.Execute(options.EvalSetPath);
}

Console.Error.WriteLine($"agent-eval: live runs (--backend {options.Backend}) need the seeded fixture actions; not implemented yet (T079a part 2).");
return 2;
