// The evaluation harness of the composer agent (feature 011, research R-09, T079a). Every run goes through the real agent
// runtime over a seeded fixture of fake StemStudio data (evaluation/eval-set.md "Seeded fixture"); nothing leaves the
// process except the model requests. `--backend fake` runs the grading self-test and then a live run in which every case
// plays its ideal transcript (no model). `--backend ollama --model qwen3.5:9b --runs 3` is the SC-001 run on the reference
// machine (CTO T081). `--backend claude` (paid, opt-in, constitution 1.3.0) is not wired in the harness.
using StemStudio.AgentEval;
using StemStudio.AgentEval.Lyric;

var (options, error) = EvalOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine(error);
    return 64;
}

if (options.Backend == "claude")
{
    Console.Error.WriteLine("agent-eval: the paid Claude backend is not wired in the harness; evaluate with --backend ollama.");
    return 2;
}

if (options.Suite == "lyric")
{
    return await LyricCommand.RunAsync(options, Console.Out);
}

var set = EvalSet.Parse(await File.ReadAllTextAsync(options.EvalSetPath));
var presets = await File.ReadAllTextAsync(options.Paths.Presets);
var prelude = set.Cases.FirstOrDefault(item => item.Id == LiveEvaluation.UndoPreludeCase);
if (options.Backend == "fake" && SelfTest.Execute(options.EvalSetPath) != 0)
{
    return 1;
}

using var http = options.Backend == "fake" ? null : EvalBackends.OllamaHttp();
var backend = http is null ? EvalBackends.Fake(prelude) : EvalBackends.Ollama(options.Model, http);
var runner = new LiveRunner(backend, presets, prelude);
var results = await LiveEvaluation.RunAsync(set, runner, options.Runs, Console.Out);
return await LiveEvaluation.ReportAsync($"{options.Backend} {backend.Model}, {options.Runs} run(s)", options.Paths.Report, results);
