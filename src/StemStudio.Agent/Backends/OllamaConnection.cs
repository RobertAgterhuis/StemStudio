namespace StemStudio.Agent.Backends;

/// <summary>
/// The one Ollama <see cref="HttpClient"/> of the process, shared by the chat client factory, the probe and the GPU release.
/// The base address always ends with a slash (relative paths like <c>api/tags</c> resolve below it); the client has no
/// timeout of its own because every call has its own bound (probe 2 s, release 2 s, turn timeout).
/// </summary>
public sealed class OllamaConnection : IDisposable
{
    /// <summary>The address used when none is configured (non-Docker path).</summary>
    public const string DefaultBaseUrl = "http://localhost:11434/";

    /// <summary>Creates the connection.</summary>
    /// <param name="baseUrl">Configured base URL, or null/blank for the default.</param>
    public OllamaConnection(string? baseUrl)
    {
        var address = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim();
        Http = new HttpClient { BaseAddress = new Uri(address.EndsWith('/') ? address : address + "/"), Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>The shared client.</summary>
    public HttpClient Http { get; }

    /// <inheritdoc />
    public void Dispose() => Http.Dispose();
}
