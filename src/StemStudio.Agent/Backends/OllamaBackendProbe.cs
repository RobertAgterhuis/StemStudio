using System.Net.Http.Json;
using System.Text.Json;
using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>What the Ollama probe found, with a readable reason and fix when the backend is not usable.</summary>
/// <param name="Status">Reachability, version and models.</param>
/// <param name="Ready">True when the default model is present and supports tools.</param>
/// <param name="Reason">Why it is not ready.</param>
/// <param name="Fix">What the user can do.</param>
public sealed record OllamaProbeResult(OllamaStatusDto Status, bool Ready, string? Reason, string? Fix);

/// <summary>
/// Probes Ollama without generating text (<c>GET /api/version</c>, <c>GET /api/tags</c>, <c>POST /api/show</c> per model for
/// the <c>tools</c> capability), within 2 s in total (FR-023). Nothing is ever pulled.
/// </summary>
/// <param name="http">HTTP client with the Ollama base address.</param>
/// <param name="time">Clock (timeout).</param>
public sealed class OllamaBackendProbe(HttpClient http, TimeProvider time)
{
    /// <summary>Probe budget.</summary>
    public static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(2);

    private static readonly Dictionary<string, string> KnownSizes = new(StringComparer.Ordinal)
    {
        [AgentSettings.DefaultOllamaModel] = "6.6 GB",
        [AgentSettings.DefaultOllamaFallbackModel] = "3.4 GB",
    };

    /// <summary>The probe's clock (latency of explicit tests).</summary>
    public TimeProvider Time => time;

    /// <summary>The pull command of a model, with the download size for the two default models.</summary>
    /// <param name="model">Model name.</param>
    /// <returns>For example <c>ollama pull qwen3.5:9b (6.6 GB)</c>.</returns>
    public static string PullCommand(string model) =>
        KnownSizes.TryGetValue(model, out var known) ? $"ollama pull {model} ({known})" : $"ollama pull {model}";

    /// <summary>Probes Ollama for the selected models.</summary>
    /// <param name="defaultModel">Default model.</param>
    /// <param name="fallbackModel">Fallback model.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The result.</returns>
    public async Task<OllamaProbeResult> ProbeAsync(string defaultModel, string fallbackModel, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return Assess(await StatusAsync(defaultModel, fallbackModel, linked.Token), defaultModel);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return Unreachable();
        }
    }

    private async Task<OllamaStatusDto> StatusAsync(string defaultModel, string fallbackModel, CancellationToken cancellationToken)
    {
        var models = await ModelsAsync(cancellationToken);
        var version = (await http.GetFromJsonAsync<VersionResponse>("api/version", cancellationToken))?.Version;
        return new OllamaStatusDto
        {
            Reachable = true,
            Version = version,
            Models = models,
            DefaultPresent = models.Any(model => model.Name == defaultModel),
            FallbackPresent = models.Any(model => model.Name == fallbackModel),
        };
    }

    private async Task<IReadOnlyList<OllamaModelInfoDto>> ModelsAsync(CancellationToken cancellationToken)
    {
        var tags = await http.GetFromJsonAsync<TagsResponse>("api/tags", cancellationToken);
        var models = tags?.Models ?? [];
        var tools = await Task.WhenAll(models.Select(model => SupportsToolsAsync(model.Name, cancellationToken)));
        return [.. models.Select((model, index) => new OllamaModelInfoDto { Name = model.Name, SizeBytes = model.Size, Tools = tools[index] })];
    }

    private async Task<bool> SupportsToolsAsync(string model, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("api/show", new { model }, cancellationToken);
        var show = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ShowResponse>(cancellationToken) : null;
        return show?.Capabilities?.Contains("tools", StringComparer.Ordinal) == true;
    }

    private static OllamaProbeResult Assess(OllamaStatusDto status, string defaultModel)
    {
        var model = status.Models.FirstOrDefault(item => item.Name == defaultModel);
        if (model is null)
        {
            return new OllamaProbeResult(status, false, $"Model {defaultModel} is not pulled.", PullCommand(defaultModel));
        }

        return model.Tools
            ? new OllamaProbeResult(status, true, null, null)
            : new OllamaProbeResult(status, false, $"Model {defaultModel} does not support tool calling.", $"Choose a model with tool support in Settings, e.g. {AgentSettings.DefaultOllamaModel}.");
    }

    private OllamaProbeResult Unreachable() => new(
        new OllamaStatusDto { Reachable = false, DefaultPresent = false, FallbackPresent = false },
        false,
        $"Local AI not reachable at {http.BaseAddress}.",
        "Start Ollama on this computer (https://ollama.com) and keep it running.");

    private sealed record VersionResponse(string? Version);

    private sealed record TagsResponse(IReadOnlyList<TagModel>? Models);

    private sealed record TagModel(string Name, long Size);

    private sealed record ShowResponse(IReadOnlyList<string>? Capabilities);
}
