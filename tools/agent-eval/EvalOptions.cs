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
    /// <summary>Suite: <c>agent</c> (default, the agent loop) or <c>lyric</c> (the lyric assistant, 023 R-09).</summary>
    public string Suite { get; init; } = "agent";

    /// <summary>Lyric suite: a performance sheet to time instead of the cases (023 T008, SC-004).</summary>
    public string? Sheet { get; init; }

    /// <summary>Lyric suite: only these case ids (comma separated on the command line); empty = all.</summary>
    public IReadOnlyList<string> Cases { get; init; } = [];

    private const string Usage = "Usage: agent-eval --backend ollama|claude|fake --runs 1..10 [--model qwen3.5:9b] [--eval-set eval-set.json] [--presets presets.v1.json] [--out report.md] [--suite agent|lyric] [--lyric-set lyric-eval-set.json] [--sheet sheet.txt] [--cases N01,N02]";
    private static readonly string[] Backends = ["ollama", "claude", "fake"];

    /// <summary>eval-set.json generated from specs/011-ai-composer-agent/evaluation/eval-set.md.</summary>
    public string EvalSetPath => Paths.EvalSet;

    /// <summary>Parses the arguments; returns null and an error text when they are invalid.</summary>
    public static (EvalOptions? Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        var values = Pairs(args);
        var backend = values.GetValueOrDefault("--backend", "ollama");
        var suite = values.GetValueOrDefault("--suite", "agent");
        if (suite is not ("agent" or "lyric") || !Backends.Contains(backend) || !int.TryParse(values.GetValueOrDefault("--runs", "3"), NumberStyles.None, CultureInfo.InvariantCulture, out var runs) || runs is < 1 or > 10)
        {
            return (null, Usage);
        }

        return (new EvalOptions(backend, runs, EvalPaths.From(values), values.GetValueOrDefault("--model", "qwen3.5:9b"))
        {
            Suite = suite,
            Sheet = values.GetValueOrDefault("--sheet"),
            Cases = values.TryGetValue("--cases", out var cases) ? cases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [],
        }, null);
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
    /// <summary>The lyric evaluation set (JSON, converted from lyric-eval-set.md).</summary>
    public string LyricSet { get; init; } = "tools/agent-eval/lyric-eval-set.json";

    /// <summary>The paths from the arguments, with defaults relative to the repository root.</summary>
    public static EvalPaths From(Dictionary<string, string> values) => new(
        values.GetValueOrDefault("--eval-set", "tools/agent-eval/eval-set.json"),
        values.GetValueOrDefault("--presets", "src/StemStudio.Domain/Styles/presets.v1.json"),
        values.GetValueOrDefault("--out", "agent-eval-report.md"))
    {
        LyricSet = values.GetValueOrDefault("--lyric-set", "tools/agent-eval/lyric-eval-set.json"),
    };
}
