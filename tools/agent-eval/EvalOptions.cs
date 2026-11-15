using System.Globalization;

namespace StemStudio.AgentEval;

/// <summary>Command line of the harness: <c>--backend ollama|claude|fake --runs N [--eval-set path] [--out path]</c>.</summary>
/// <param name="Backend">Backend under test; <c>claude</c> is paid and runs only on the owner's explicit command.</param>
/// <param name="Runs">Repetitions per case (SC-001 uses the median of 3).</param>
/// <param name="EvalSetPath">eval-set.json generated from specs/011-ai-composer-agent/evaluation/eval-set.md.</param>
/// <param name="ReportPath">Markdown report to write.</param>
internal sealed record EvalOptions(string Backend, int Runs, string EvalSetPath, string ReportPath)
{
    private static readonly string[] Backends = ["ollama", "claude", "fake"];

    /// <summary>Parses the arguments; returns null and an error text when they are invalid.</summary>
    public static (EvalOptions? Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i + 1 < args.Count; i += 2)
        {
            values[args[i]] = args[i + 1];
        }

        var backend = values.GetValueOrDefault("--backend", "ollama");
        if (!Backends.Contains(backend) || !int.TryParse(values.GetValueOrDefault("--runs", "3"), NumberStyles.None, CultureInfo.InvariantCulture, out var runs) || runs is < 1 or > 10)
        {
            return (null, "Usage: agent-eval --backend ollama|claude|fake --runs 1..10 [--eval-set eval-set.json] [--out report.md]");
        }

        return (new EvalOptions(backend, runs, values.GetValueOrDefault("--eval-set", "eval-set.json"), values.GetValueOrDefault("--out", "agent-eval-report.md")), null);
    }
}
