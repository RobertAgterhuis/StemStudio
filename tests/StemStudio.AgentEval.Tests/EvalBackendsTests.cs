using StemStudio.Agent.Backends;
using StemStudio.AgentEval.Fixture;
using StemStudio.Contracts.Agent;

namespace StemStudio.AgentEval.Tests;

/// <summary>The harness backends (no model is called) and the evaluation set parser.</summary>
[Collection(EnvironmentVariables.Name)]
public sealed class EvalBackendsTests
{
    [Fact]
    public void OllamaHttp_UsesTheAppsVariable_WithATrailingSlash_OrLocalhost()
    {
        var saved = Environment.GetEnvironmentVariable(EvalBackends.OllamaUrlVariable);
        try
        {
            Environment.SetEnvironmentVariable(EvalBackends.OllamaUrlVariable, "http://gpu-box:11434");
            using (var configured = EvalBackends.OllamaHttp())
            {
                configured.BaseAddress.ShouldBe(new Uri("http://gpu-box:11434/"));
                configured.Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
            }

            Environment.SetEnvironmentVariable(EvalBackends.OllamaUrlVariable, " ");
            using var fallback = EvalBackends.OllamaHttp();
            fallback.BaseAddress.ShouldBe(new Uri("http://localhost:11434/"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(EvalBackends.OllamaUrlVariable, saved);
        }
    }

    [Fact]
    public void Ollama_SelectsThePrimaryModel_PerCase()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:9/") };

        var backend = EvalBackends.Ollama("qwen3.5:9b", http);

        (backend.Backend, backend.Model).ShouldBe((AgentBackendDto.Ollama, "qwen3.5:9b"));
        backend.Session(Repo.Case("1"), new FixtureIds()).ShouldBeOfType<AgentModelSelector>();
    }

    [Fact]
    public void Script_OfACaseWithoutCalls_IsOneClosingText()
    {
        EvalBackends.Script(Repo.Case("21"), new FixtureIds()).ShouldBe("""{"version":1,"turns":[{"text":"I cannot do that here."}]}""");
    }

    [Fact]
    public void Parse_RejectsAnEmptySet()
    {
        Should.Throw<InvalidDataException>(() => EvalSet.Parse("null"));
    }
}

/// <summary>Tests that change process environment variables run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariables
{
    public const string Name = "environment";
}
