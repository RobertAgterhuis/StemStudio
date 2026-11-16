namespace StemStudio.AgentEval.Tests;

/// <summary>Skeleton tests (architect, 011 follow-ups): the harness command line and its exit codes.</summary>
public sealed class EvalOptionsTests
{
    [Fact]
    public void Parse_DefaultsToOllamaThreeRuns()
    {
        var (options, error) = EvalOptions.Parse([]);

        error.ShouldBeNull();
        options.ShouldBe(new EvalOptions("ollama", 3, "eval-set.json", "agent-eval-report.md"));
    }

    [Fact]
    public void Parse_ReadsEveryOption()
    {
        var (options, _) = EvalOptions.Parse(["--backend", "fake", "--runs", "10", "--eval-set", "set.json", "--out", "r.md"]);

        options.ShouldBe(new EvalOptions("fake", 10, "set.json", "r.md"));
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
    public void EntryPoint_ReturnsUsageErrorForInvalidArguments()
    {
        Run("--backend", "openai").ShouldBe(64);
    }

    [Fact]
    public void EntryPoint_LiveBackendIsNotImplementedYet()
    {
        Run("--backend", "ollama", "--runs", "1").ShouldBe(2);
    }

    private static int Run(params string[] args) =>
        (int)typeof(EvalOptions).Assembly.EntryPoint!.Invoke(null, [args])!;
}
