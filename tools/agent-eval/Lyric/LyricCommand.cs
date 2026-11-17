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
        var model = Client(options.Model, http);
        var grades = await LyricSuite.RunAsync(set, _ => model, options.Runs, log, CancellationToken.None);
        await File.WriteAllTextAsync(options.Paths.Report, LyricGrader.Report($"ollama {options.Model}, {options.Runs} run(s), harness verification (no dictionary layer)", grades));
        await log.WriteLineAsync($"lyric suite: {grades.Count} case runs; report {options.Paths.Report}.");
        return 0;
    }

    /// <summary>The real model client on Ollama only (the Claude factory has no key source and is never selected).</summary>
    /// <param name="model">Ollama model.</param>
    /// <param name="http">Ollama HTTP client.</param>
    /// <returns>The client.</returns>
    public static LyricModelClient Client(string model, HttpClient http) => new(
        new FixedSettingsStore(AgentSettings.Defaults(AgentBackendDto.Ollama, DateTimeOffset.UtcNow) with { OllamaModel = model }),
        new IdleGpuActivity(),
        new LyricModelBackends
        {
            Ollama = new OllamaChatClientFactory(http),
            Claude = new ClaudeChatClientFactory(new ClaudeKeyProvider(new NoEnvironment(), NoStoredClaudeKeyReader.Instance)),
        });

    private sealed class NoEnvironment : Agent.Ports.IEnvironmentReader
    {
        public string? GetVariable(string name) => null;
    }
}
