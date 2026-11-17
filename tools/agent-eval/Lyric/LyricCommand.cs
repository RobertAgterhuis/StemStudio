using StemStudio.Agent.Backends;
using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.Model;
using StemStudio.Agent.Runtime;
using StemStudio.AgentEval.Fixture;
using StemStudio.Contracts.Agent;

namespace StemStudio.AgentEval.Lyric;

/// <summary>
/// <c>--suite lyric</c>: with <c>--backend fake</c> the grading self-test (CI); with <c>--backend ollama</c> the reference-machine
/// run of lyric-eval-set.json against the local model (023 T068). Claude is never used here.
/// </summary>
internal static class LyricCommand
{
    /// <summary>Runs the suite.</summary>
    /// <param name="options">Command line.</param>
    /// <param name="log">Progress log.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(EvalOptions options, TextWriter log)
    {
        var set = LyricEvalSet.Parse(await File.ReadAllTextAsync(options.Paths.LyricSet));
        if (options.Backend == "fake")
        {
            return (await LyricSuite.SelfTestAsync(set, log, CancellationToken.None)).Count == 0 ? 0 : 1;
        }

        using var http = EvalBackends.OllamaHttp();
        var model = Client(options.Model, http, options.Cpu);
        if (options.Sheet is { } sheet)
        {
            return await PerformanceAsync(options, sheet, model, log);
        }

        if (options.Cases.Count > 0)
        {
            set = set with { Cases = [.. set.Cases.Where(item => options.Cases.Contains(item.Id))] };
        }

        var grades = await LyricSuite.RunAsync(set, _ => model, new LyricSuiteRuns(options.Runs, log), CancellationToken.None);
        await File.WriteAllTextAsync(options.Paths.Report, LyricGrader.Report($"ollama {options.Model}, {options.Runs} run(s), harness verification (no dictionary layer)", grades));
        await log.WriteLineAsync($"lyric suite: {grades.Count} case runs; report {options.Paths.Report}.");
        return 0;
    }

    /// <summary>Times a performance sheet <c>--runs</c> times (the language from the file name: <c>.nl.</c> or <c>.en.</c>).</summary>
    /// <param name="options">Command line.</param>
    /// <param name="sheet">Sheet path.</param>
    /// <param name="model">Model.</param>
    /// <param name="log">Log.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> PerformanceAsync(EvalOptions options, string sheet, ILyricModel model, TextWriter log)
    {
        var text = await File.ReadAllTextAsync(sheet);
        var language = sheet.Contains(".en.", StringComparison.Ordinal) ? Contracts.LyricAssist.AssistLanguageDto.En : Contracts.LyricAssist.AssistLanguageDto.Nl;
        var runs = new List<PerformanceRun>();
        for (var run = 0; run < options.Runs; run++)
        {
            runs.Add(await LyricPerformance.RunAsync(text, language, model, CancellationToken.None));
            await log.WriteLineAsync($"performance run {run + 1}: first block {runs[^1].FirstBlockSeconds:0.0} s, total {runs[^1].TotalSeconds:0.0} s, valid JSON {runs[^1].ValidReplies}/{runs[^1].Replies}");
        }

        await File.WriteAllTextAsync(options.Paths.Report, LyricPerformance.Report($"{Path.GetFileName(sheet)}, ollama {options.Model}", runs));
        return 0;
    }

    /// <summary>The real model client on Ollama only (the Claude factory has no key source and is never selected).</summary>
    /// <param name="model">Ollama model.</param>
    /// <param name="http">Ollama HTTP client.</param>
    /// <param name="cpu">True: the model runs as the CPU fallback.</param>
    /// <returns>The client.</returns>
    public static LyricModelClient Client(string model, HttpClient http, bool cpu = false) => new(
        new FixedSettingsStore(AgentSettings.Defaults(AgentBackendDto.Ollama, DateTimeOffset.UtcNow) with { OllamaModel = model, OllamaFallbackModel = model }),
        cpu ? new BusyGpu() : new IdleGpuActivity(),
        new LyricModelBackends
        {
            Ollama = new OllamaChatClientFactory(http),
            Claude = new ClaudeChatClientFactory(new ClaudeKeyProvider(new NoEnvironment(), NoStoredClaudeKeyReader.Instance)),
        });

    private sealed class BusyGpu : Agent.Ports.IGpuActivity
    {
        public bool IsHeavyJobActive() => true;
    }

    private sealed class NoEnvironment : Agent.Ports.IEnvironmentReader
    {
        public string? GetVariable(string name) => null;
    }
}
