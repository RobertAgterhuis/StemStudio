using System.Net.Http.Json;
using StemStudio.Agent.Ports;

namespace StemStudio.Agent.Backends;

/// <summary>
/// Unloads both agent models from the GPU (<c>POST /api/generate</c> with <c>keep_alive: 0</c>) before the API submits a
/// heavy GPU job (contracts/DELTA.md section 5, FR-022). Best effort: at most 2 s, never throws.
/// </summary>
/// <param name="http">HTTP client with the Ollama base address.</param>
/// <param name="settings">Agent settings (model names).</param>
/// <param name="time">Clock (timeout).</param>
public sealed class OllamaGpuRelease(HttpClient http, IAgentSettingsStore settings, TimeProvider time) : ILlmGpuRelease
{
    /// <summary>Release budget.</summary>
    public static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await UnloadAsync(linked.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // Best effort: the job starts anyway; Ollama frees the memory when it unloads on its own.
        }
    }

    private async Task UnloadAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        foreach (var model in new[] { current.OllamaModel, current.OllamaFallbackModel }.Distinct(StringComparer.Ordinal))
        {
            using var response = await http.PostAsJsonAsync("api/generate", new { model, keep_alive = 0 }, cancellationToken);
        }
    }
}
