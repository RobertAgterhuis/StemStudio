using System.Globalization;

namespace StemStudio.AgentEval;

/// <summary>
/// Command line of the harness:
/// <c>--backend ollama|claude|fake --runs N [--model name] [--eval-set path] [--presets path] [--out path]</c>.
/// </summary>
/// <param name="Backend">Backend under test; <c>claude</c> is paid and runs only on the owner's explicit command.</param>
/// <param name="Runs">Repetitions per case (SC-001 uses the median of 3).</param>
/// <param name="Paths">Input and output files.</param>
/// <param name="Model">Ollama model under test (default the app's primary model).</param>
internal sealed record EvalOptions(string Backend, int Runs, EvalPaths Paths, string Model)
{
    private const string Usage = "Usage: agent-eval --backend ollama|claude|fake --runs 1..10 [--model qwen3.5:9b] [--eval-set eval-set.json] [--presets presets.v1.json] [--out report.md]";
    private static readonly string[] Backends = ["ollama", "claude", "fake"];

    /// <summary>eval-set.json generated from specs/011-ai-composer-agent/evaluation/eval-set.md.</summary>
    public string EvalSetPath => Paths.EvalSet;

    /// <summary>Parses the arguments; returns null and an error text when they are invalid.</summary>
    public static (EvalOptions? Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        var values = Pairs(args);
        var backend = values.GetValueOrDefault("--backend", "ollama");
        if (!Backends.Contains(backend) || !int.TryParse(values.GetValueOrDefault("--runs", "3"), NumberStyles.None, CultureInfo.InvariantCulture, out var runs) || runs is < 1 or > 10)
        {
            return (null, Usage);
        }

        return (new EvalOptions(backend, runs, EvalPaths.From(values), values.GetValueOrDefault("--model", "qwen3.5:9b")), null);
    }

    private static Dictionary<string, string> Pairs(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i + 1 < args.Count; i += 2)
        {
            values[args[i]] = args[i + 1];
        }

        return values;
    }
}

/// <summary>Files of an evaluation.</summary>
/// <param name="EvalSet">The evaluation set (JSON).</param>
/// <param name="Presets">The style presets of feature 009 (seeded as the fixture's styles).</param>
/// <param name="Report">Markdown report to write.</param>
internal sealed record EvalPaths(string EvalSet, string Presets, string Report)
{
    /// <summary>The paths from the arguments, with defaults relative to the repository root.</summary>
    public static EvalPaths From(Dictionary<string, string> values) => new(
        values.GetValueOrDefault("--eval-set", "tools/agent-eval/eval-set.json"),
        values.GetValueOrDefault("--presets", "src/StemStudio.Domain/Styles/presets.v1.json"),
        values.GetValueOrDefault("--out", "agent-eval-report.md"));
}
