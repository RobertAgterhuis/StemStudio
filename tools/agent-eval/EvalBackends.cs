using System.Text.Json.Nodes;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Backends.Fakes;
using StemStudio.Agent.Model;
using StemStudio.AgentEval.Fixture;
using StemStudio.Contracts.Agent;

namespace StemStudio.AgentEval;

/// <summary>The chat backends of the harness: the scripted fake (no model) and the local Ollama model.</summary>
internal static class EvalBackends
{
    /// <summary>Environment variable of the Ollama base URL (same as the app).</summary>
    public const string OllamaUrlVariable = "StemStudio__Agent__Ollama__BaseUrl";

    private const string DefaultOllamaUrl = "http://localhost:11434/";

    /// <summary>
    /// The fake backend: every case plays its ideal transcript (all expected calls in one turn, then a closing text), so a
    /// fake live run checks the fixture, the runtime wiring, the approval answers and the grading end to end without a model.
    /// </summary>
    public static LiveBackend Fake(EvalCase? undoPrelude) =>
        new(AgentBackendDto.Ollama, ScriptedChatClient.ModelId, (evalCase, ids) => new ScriptedChatSession(Script(evalCase.Undo && undoPrelude is not null ? undoPrelude : evalCase, ids)));

    /// <summary>The local Ollama model (primary model only: the harness reports no GPU job, so no fallback).</summary>
    public static LiveBackend Ollama(string model, HttpClient http)
    {
        var factory = new OllamaChatClientFactory(http);
        var settings = new FixedSettingsStore(AgentSettings.Defaults(AgentBackendDto.Ollama, DateTimeOffset.UtcNow) with { OllamaModel = model });
        return new(AgentBackendDto.Ollama, model, (_, _) => new AgentModelSelector(settings, new IdleGpu(), new ChatBackends { Ollama = factory }));
    }

    /// <summary>The HTTP client of the Ollama backend (base URL from <see cref="OllamaUrlVariable"/>, default localhost).</summary>
    public static HttpClient OllamaHttp()
    {
        var url = Environment.GetEnvironmentVariable(OllamaUrlVariable);
        var baseUrl = string.IsNullOrWhiteSpace(url) ? DefaultOllamaUrl : url.TrimEnd('/') + "/";
        return new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>The ideal transcript of a case as an agent script (contracts/agent-script.schema.json), with fixture ids.</summary>
    internal static string Script(EvalCase evalCase, FixtureIds ids)
    {
        var calls = new JsonArray([.. SelfTest.Ideal(evalCase).Select(call => (JsonNode)new JsonObject { ["name"] = call.Tool, ["arguments"] = JsonNode.Parse(call.Arguments) })]);
        JsonArray turns = calls.Count == 0
            ? [new JsonObject { ["text"] = "I cannot do that here." }]
            : [new JsonObject { ["calls"] = calls }, new JsonObject { ["text"] = "Done." }];
        return ids.ToIds(new JsonObject { ["version"] = 1, ["turns"] = turns }.ToJsonString());
    }
}
