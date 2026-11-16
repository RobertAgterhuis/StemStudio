namespace StemStudio.AgentEval.Tests;

/// <summary>The harness command line and its exit codes for arguments that never reach a model.</summary>
public sealed class EvalOptionsTests
{
    [Fact]
    public void Parse_DefaultsToOllamaThreeRunsAndTheRepositoryPaths()
    {
        var (options, error) = EvalOptions.Parse([]);

        error.ShouldBeNull();
        options.ShouldBe(new EvalOptions("ollama", 3, new EvalPaths("tools/agent-eval/eval-set.json", "src/StemStudio.Domain/Styles/presets.v1.json", "agent-eval-report.md"), "qwen3.5:9b"));
        options!.EvalSetPath.ShouldBe("tools/agent-eval/eval-set.json");
    }

    [Fact]
    public void Parse_ReadsEveryOption_AndIgnoresATrailingName()
    {
        var (options, _) = EvalOptions.Parse(["--backend", "fake", "--runs", "10", "--model", "m", "--eval-set", "set.json", "--presets", "p.json", "--out", "r.md", "--runs"]);

        options.ShouldBe(new EvalOptions("fake", 10, new EvalPaths("set.json", "p.json", "r.md"), "m"));
    }

    [Theory]
    [InlineData("--backend", "openai")]
    [InlineData("--runs", "0")]
    [InlineData("--runs", "11")]
    [InlineData("--runs", "-1")]
    [InlineData("--runs", "three")]
    public void Parse_RejectsInvalidValues(string name, string value)
    {
        var (options, error) = EvalOptions.Parse([name, value]);

        options.ShouldBeNull();
        error.ShouldStartWith("Usage: agent-eval");
    }

    [Fact]
    public async Task EntryPoint_ReturnsUsageErrorForInvalidArguments()
    {
        (await EntryPoint.RunAsync("--backend", "openai")).ShouldBe(64);
    }

    [Fact]
    public async Task EntryPoint_TheClaudeBackendIsNotWired()
    {
        (await EntryPoint.RunAsync("--backend", "claude", "--runs", "1")).ShouldBe(2);
    }
}
