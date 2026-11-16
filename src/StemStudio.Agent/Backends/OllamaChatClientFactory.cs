using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace StemStudio.Agent.Backends;

/// <summary>
/// Creates Ollama chat clients over one <see cref="HttpClient"/> (base address = <c>StemStudio__Agent__Ollama__BaseUrl</c>)
/// and the per-turn options verified in research F-13: <c>num_ctx</c> 16,384, <c>num_gpu</c> 0 for the CPU fallback,
/// top-level <c>think</c> and <c>keep_alive</c>.
/// </summary>
/// <param name="http">HTTP client with the Ollama base address.</param>
public sealed class OllamaChatClientFactory(HttpClient http)
{
    /// <summary>Context window of every request.</summary>
    public const int ContextTokens = 16_384;

    /// <summary>How long Ollama keeps the model loaded after a turn.</summary>
    public const string KeepAlive = "10m";

    /// <summary>The Ollama base address (for status texts).</summary>
    public Uri? BaseAddress => http.BaseAddress;

    /// <summary>A chat client for a model.</summary>
    /// <param name="model">Ollama model name.</param>
    /// <returns>The client.</returns>
    public IChatClient Create(string model) => new OllamaApiClient(http, model);

    /// <summary>The backend options of a turn.</summary>
    /// <param name="think">Ollama thinking on or off (setting).</param>
    /// <param name="cpuOnly">True for the CPU fallback while a GPU job runs.</param>
    /// <returns>The options (the runner adds tools and tool mode).</returns>
    public static ChatOptions Options(bool think, bool cpuOnly)
    {
        var options = new ChatOptions();
        options.AddOllamaOption(OllamaOption.NumCtx, ContextTokens);
        if (cpuOnly)
        {
            options.AddOllamaOption(OllamaOption.NumGpu, 0);
        }

        options.RawRepresentationFactory = _ => new ChatRequest { Think = think, KeepAlive = KeepAlive };
        return options;
    }
}
