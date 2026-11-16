using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>
/// Agent settings of the API host, bound from <c>StemStudio:Agent</c> (environment <c>StemStudio__Agent__*</c>, data-model 1).
/// The Ollama and Claude options belong to the agent library; the API key is never configuration.
/// </summary>
public sealed class AgentHostOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "StemStudio:Agent";

    /// <summary>Initial backend when no settings row exists: ollama (default), claude, none or fake.</summary>
    public string? Backend { get; set; }

    /// <summary>Base URL of the API's own public endpoints for the server tools; unset = the first Kestrel address.</summary>
    public Uri? LoopbackBaseUrl { get; set; }

    /// <summary>The configured initial backend; unknown values fall back to Ollama.</summary>
    /// <returns>The backend.</returns>
    public AgentBackendDto InitialBackend() =>
        Enum.TryParse<AgentBackendDto>(Backend, ignoreCase: true, out var backend) && Enum.IsDefined(backend) ? backend : AgentBackendDto.Ollama;
}
